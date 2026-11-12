using System;
using System.Threading;
using System.Threading.Tasks;
using FP2.Captions;
using Meta.XR;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering;

/// <summary>Local hand poses and opt-in, transient camera samples share one effect gate.</summary>
public sealed class ThumbsUpEffects : MonoBehaviour
{
    [SerializeField] private FingerFeatureStateProvider leftHand;
    [SerializeField] private FingerFeatureStateProvider rightHand;
    [SerializeField] private ShapeRecognizerActiveState leftShape;
    [SerializeField] private ShapeRecognizerActiveState rightShape;
    [SerializeField] private TransformRecognizerActiveState leftOrientation;
    [SerializeField] private TransformRecognizerActiveState rightOrientation;
    [SerializeField] private SkinnedMeshRenderer[] handVisualRenderers = Array.Empty<SkinnedMeshRenderer>();
    [SerializeField] private PassthroughCameraAccess cameraAccess;
    [SerializeField] private RectTransform effectRoot;
    [SerializeField] private CanvasGroup effectOpacity;
    [SerializeField, Min(2.5f)] private float sampleInterval = 3f;

    private readonly ThumbsUpGate gate = new ThumbsUpGate();
    private Func<byte[], CancellationToken, Task<bool>> classifier;
    private CancellationTokenSource requestCancellation;
    private PermissionCallbacks permissionCallbacks;
    private bool cameraMode;
    private bool requestingPermission;
    private bool inFlight;
    private int generation;
    private double nextSample;
    private double enabledAt;
    private double popStarted = double.NegativeInfinity;
    private Vector2 effectPosition;
    private string cameraStatus = "Camera off";
    private string handsStatus = "Hands: waiting for tracking";

    public string Status { get; private set; } = "Hands: waiting for tracking | Camera off";
    public Func<bool> CanSample { get; set; }
    public event Action StatusChanged;
    public event Action NativeThumbsUp;

    public void Configure(Func<byte[], CancellationToken, Task<bool>> classifier)
    {
        StopCamera("Camera off");
        this.classifier = classifier;
    }

    private void Awake()
    {
        // SDK hand visuals toggle Renderer.enabled as tracking changes. This separate render-only
        // flag keeps passthrough clear without disabling skeleton updates or hand-pose providers.
        foreach (var renderer in handVisualRenderers)
            if (renderer != null) renderer.forceRenderingOff = true;
        if (cameraAccess != null) cameraAccess.enabled = false;
        if (effectRoot != null) effectPosition = effectRoot.anchoredPosition;
        if (effectOpacity != null) effectOpacity.alpha = 0;
    }

    public void ToggleCameraMode()
    {
        if (!isActiveAndEnabled) return;
        if (cameraMode) { StopCamera("Camera off"); return; }
        if (classifier == null) { SetCameraStatus("Camera unavailable: service not configured"); return; }
        if (cameraAccess == null || !SystemInfo.supportsAsyncGPUReadback)
        { SetCameraStatus("Camera unavailable on this device"); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!PassthroughCameraAccess.IsSupported)
        { SetCameraStatus("Camera needs Quest 3/3S and supported Horizon OS"); return; }
        cameraMode = true;
        int current = ++generation;
        enabledAt = Time.unscaledTimeAsDouble;
        nextSample = enabledAt;
        gate.Reset(ThumbsUpSource.Camera);
        if (Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.PassthroughCameraAccessPermission))
        {
            StartCamera();
            return;
        }
        requestingPermission = true;
        SetCameraStatus("Camera permission requested");
        permissionCallbacks = new PermissionCallbacks();
        permissionCallbacks.PermissionGranted += _ =>
        {
            if (!IsCurrent(current)) return;
            requestingPermission = false;
            StartCamera();
        };
        permissionCallbacks.PermissionDenied += _ =>
        {
            if (IsCurrent(current)) StopCamera("Camera permission denied; toggle to retry");
        };
        Permission.RequestUserPermission(OVRPermissionsRequester.PassthroughCameraAccessPermission, permissionCallbacks);
#else
        SetCameraStatus("Camera mode requires the Quest Android build");
#endif
    }

    private void StartCamera()
    {
        if (!cameraMode || cameraAccess == null) return;
        enabledAt = Time.unscaledTimeAsDouble;
        cameraAccess.enabled = true;
        SetCameraStatus("Camera on: starting; frames sent for gesture checks");
    }

    private void Update()
    {
        double now = Time.unscaledTimeAsDouble;
        bool? left = ReadPose(leftHand, leftShape, leftOrientation);
        bool? right = ReadPose(rightHand, rightShape, rightOrientation);
        if (gate.Observe(ThumbsUpSource.LeftHand, left, now)) { if (NativeThumbsUp != null) NativeThumbsUp.Invoke(); else Pop(now); }
        if (gate.Observe(ThumbsUpSource.RightHand, right, now)) { if (NativeThumbsUp != null) NativeThumbsUp.Invoke(); else Pop(now); }
        string tracking = left.HasValue || right.HasValue ? "Hands: tracked" : "Hands: waiting for tracking";
        if (tracking != handsStatus) { handsStatus = tracking; PublishStatus(); }
        AnimatePop(now);
        if (!cameraMode || requestingPermission || inFlight || cameraAccess == null) return;
        if (!cameraAccess.IsPlaying)
        {
            if (now - enabledAt > 12) StopCamera("Camera unavailable; toggle to retry");
            return;
        }
        if (now < nextSample || !cameraAccess.IsUpdatedThisFrame || (CanSample != null && !CanSample())) return;
        nextSample = now + Mathf.Max(2.5f, sampleInterval);
        _ = SampleAsync(generation);
    }

    private static bool? ReadPose(FingerFeatureStateProvider provider,
        ShapeRecognizerActiveState shape, TransformRecognizerActiveState orientation)
    {
        if (provider == null || !provider.isActiveAndEnabled || shape == null || orientation == null) return null;
        IHand hand = provider.Hand;
        if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.IsHighConfidence) return null;
        for (int i = 0; i < 5; i++)
            if (!hand.GetFingerIsHighConfidence((HandFinger)i)) return null;
        return shape.Active && orientation.Active;
    }

    private async Task SampleAsync(int current)
    {
        if (inFlight || !IsCurrent(current)) return;
        inFlight = true;
        var operation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        requestCancellation = operation;
        RenderTexture resized = null;
        Texture2D pixels = null;
        byte[] jpeg = null;
        try
        {
            double capturedAt = Time.unscaledTimeAsDouble;
            bool nativeHeldAtCapture = gate.NativeGestureIsHeld;
            Texture source = cameraAccess.GetTexture();
            if (source == null || source.width < 1 || source.height < 1) return;
            int width = Mathf.Min(640, source.width);
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * (width / (float)source.width)));
            resized = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, resized);
            var readback = AsyncGPUReadback.Request(resized, 0, TextureFormat.RGBA32);
            // Keep the temporary render target alive until the GPU is finished, even after cancellation.
            while (!readback.done) await Task.Yield();
            if (!IsCurrent(current) || operation.IsCancellationRequested) return;
            if (readback.hasError) { SetCameraStatus("Camera read failed; retrying"); return; }
            pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            pixels.SetPixelData(readback.GetData<Color32>(), 0);
            pixels.Apply(false, false);
            jpeg = pixels.EncodeToJPG(70);
            Destroy(pixels);
            pixels = null;
            RenderTexture.ReleaseTemporary(resized);
            resized = null;
            SetCameraStatus("Camera on: checking gesture");
            bool detected = await classifier(jpeg, operation.Token);
            if (!IsCurrent(current) || operation.IsCancellationRequested) return;
            if (gate.Observe(ThumbsUpSource.Camera, detected, Time.unscaledTimeAsDouble, capturedAt, nativeHeldAtCapture))
                Pop(Time.unscaledTimeAsDouble);
            SetCameraStatus("Camera on: frames sent for gesture checks");
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(current)) SetCameraStatus("Camera check timed out; retrying");
        }
        catch (Exception)
        {
            // Provider errors can contain private data. Publish only a fixed local message.
            if (IsCurrent(current)) SetCameraStatus("Camera check failed; retrying");
        }
        finally
        {
            if (pixels != null) Destroy(pixels);
            if (resized != null) RenderTexture.ReleaseTemporary(resized);
            if (jpeg != null) Array.Clear(jpeg, 0, jpeg.Length);
            if (requestCancellation == operation) requestCancellation = null;
            operation.Dispose();
            inFlight = false;
        }
    }

    private bool IsCurrent(int current) => this != null && isActiveAndEnabled && cameraMode && current == generation;

    private void StopCamera(string reason)
    {
        cameraMode = false;
        requestingPermission = false;
        generation++;
        requestCancellation?.Cancel();
        if (cameraAccess != null) cameraAccess.enabled = false;
        gate.Reset(ThumbsUpSource.Camera);
        SetCameraStatus(reason);
    }

    private void Pop(double now) => popStarted = now;

    private void AnimatePop(double now)
    {
        if (effectRoot == null || effectOpacity == null) return;
        float t = (float)((now - popStarted) / 1.1);
        if (t >= 1 || t < 0) { effectOpacity.alpha = 0; return; }
        float scale = t < .18f ? Mathf.Lerp(.35f, 1.15f, t / .18f) : Mathf.Lerp(1.15f, 1f, (t - .18f) / .82f);
        effectRoot.localScale = Vector3.one * scale;
        effectRoot.anchoredPosition = effectPosition + new Vector2(0, 30 * t);
        effectRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2) * 7 * (1 - t));
        effectOpacity.alpha = t < .7f ? 1 : 1 - (t - .7f) / .3f;
    }

    private void SetCameraStatus(string value)
    {
        if (cameraStatus == value) return;
        cameraStatus = value;
        PublishStatus();
    }

    private void PublishStatus()
    {
        Status = handsStatus + " | " + cameraStatus;
        StatusChanged?.Invoke();
    }

    private void OnDisable()
    {
        StopCamera("Camera off");
        if (effectOpacity != null) effectOpacity.alpha = 0;
        popStarted = double.NegativeInfinity;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && !requestingPermission) StopCamera("Camera off: app paused");
    }

    private void OnDestroy() => StatusChanged = null;
}

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using FP2.Captions;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class QuestLiveCaptions : MonoBehaviour
{
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private RectTransform panel;
    [SerializeField, Range(1, 15)] private int maxRecordingSeconds = 15;
    [SerializeField] private int sampleRate = 16000;
    private CaptionSession session;
    private AudioClip recording;
    private string microphone;
    private float recordingStarted;
    private bool upper;
    private string routeLabel = "Direct UMN HTTPS";
    private long captureEnded;
    private int lastShownRevision;
    private int page = 1;

    public static string KeyPath => Application.isEditor
        ? Environment.GetEnvironmentVariable("UMN_GATEWAY_KEY_FILE") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../.secrets/umn-key.txt"))
        : Path.Combine(ConfigurationDirectory, "umn-key.txt");

    private static string ConfigurationDirectory
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var directory = activity.Call<AndroidJavaObject>("getFilesDir");
            return directory.Call<string>("getAbsolutePath");
#else
            return Application.persistentDataPath;
#endif
        }
    }

    private void Awake()
    {
        if (captionText == null || statusText == null || hintText == null || panel == null)
        { Debug.LogError("[FP2] Caption scene references missing."); enabled = false; return; }
        captionText.text = "English translations will appear here.";
        statusText.text = "Ready to configure";
        UpdateHint();
    }

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
        yield return null;
        ConfigureGateway();
    }

    public void ConfigureGateway()
    {
        if (session != null) return;
        try
        {
            string key = File.Exists(KeyPath) ? File.ReadAllText(KeyPath).Trim() : "";
            if (string.IsNullOrWhiteSpace(key)) { statusText.text = "Course gateway key not configured."; return; }
            string endpoint = "https://api.aigateway.umn.edu";
            string routePath = Path.Combine(ConfigurationDirectory, "gateway-url.txt");
            if (Debug.isDebugBuild && File.Exists(routePath) && File.ReadAllText(routePath).Trim() == "http://127.0.0.1:8765")
            { endpoint = "http://127.0.0.1:8765"; routeLabel = "USB / laptop VPN relay"; }
            session = new CaptionSession(new MandarinCaptionGateway(endpoint, key));
            session.Changed += RefreshView;
            RefreshView();
            Debug.Log("[FP2] Ready. Route=" + routeLabel);
        }
        catch (IOException) { statusText.text = "Cannot read the local course key."; }
    }

    private void Update()
    {
        if (recording != null && Time.realtimeSinceStartup - recordingStarted >= maxRecordingSeconds) StopListening();
    }

    [ContextMenu("Begin Listening")]
    public void BeginListening()
    {
        Debug.Log("[FP2] Trigger begin state=" + (session == null ? "Unconfigured" : session.State.ToString()));
        if (!isActiveAndEnabled) return;
        if (session == null) ConfigureGateway();
        if (session == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            statusText.text = "Allow microphone access, then hold the trigger again.";
            return;
        }
#endif
        if (Microphone.devices.Length == 0) { statusText.text = "No microphone available."; return; }
        if (!session.BeginCapture()) return;
        microphone = Microphone.devices[0];
        try
        {
            recording = Microphone.Start(microphone, false, maxRecordingSeconds, sampleRate);
            if (recording == null) throw new InvalidOperationException();
            recordingStarted = Time.realtimeSinceStartup;
            Debug.Log("[FP2] Microphone start active=" + Microphone.IsRecording(microphone) +
                " frequency=" + recording.frequency + " capacity_frames=" + recording.samples);
        }
        catch (Exception) { session.RejectCapture("Microphone did not start. Check permission and retry."); }
    }

    [ContextMenu("Stop Listening")]
    public void StopListening()
    {
        if (recording == null || session == null) return;
        captureEnded = Stopwatch.GetTimestamp();
        bool microphoneActive = Microphone.IsRecording(microphone);
        int frames = microphoneActive ? Microphone.GetPosition(microphone) : recording.samples;
        Debug.Log("[FP2] Trigger stop elapsed_s=" + (Time.realtimeSinceStartup - recordingStarted).ToString("F3") +
            " active=" + microphoneActive + " position_frames=" + frames + " frequency=" + recording.frequency);
        Microphone.End(microphone);
        AudioClip captured = recording;
        recording = null;
        try
        {
            if (frames < captured.frequency / 4)
            { session.RejectCapture("Too short. Hold the trigger while someone speaks."); return; }
            float[] samples = new float[frames * captured.channels];
            captured.GetData(samples, 0);
            double energy = 0;
            foreach (float sample in samples) energy += sample * sample;
            double rms = Math.Sqrt(energy / Math.Max(samples.Length, 1));
            Debug.Log("[FP2] Capture seconds=" + ((double)frames / captured.frequency).ToString("F2") + " rms=" + rms.ToString("F5"));
            if (rms < 0.001)
            { session.RejectCapture("Audio is too quiet. Move closer and try again."); return; }
            byte[] wav = WavUtility.FromAudioClip(captured, frames);
            _ = session.SubmitAsync(wav);
        }
        catch (Exception) { session.RejectCapture("Could not process microphone audio. Please retry."); }
        finally { Destroy(captured); }
    }

    public void CancelCurrentTurn()
    {
        Debug.Log("[FP2] Cancel state=" + (session == null ? "Unconfigured" : session.State.ToString()) + " recording=" + (recording != null));
        if (recording != null)
        {
            Microphone.End(microphone);
            Destroy(recording); recording = null;
        }
        session?.Cancel();
    }

    public void ToggleHeight()
    {
        upper = !upper;
        panel.localPosition = new Vector3(0, upper ? -0.25f : -0.50f, 1.5f);
    }

    public void NextPage()
    {
        captionText.ForceMeshUpdate();
        int count = Math.Max(1, captionText.textInfo.pageCount);
        page = page % count + 1;
        captionText.pageToDisplay = page;
        UpdateHint();
    }

    private void RefreshView()
    {
        statusText.text = session.Status + "  |  " + routeLabel;
        statusText.color = session.State == CaptionState.Error ? new Color(1f, .72f, .55f) : new Color(.65f, .93f, .83f);
        if (session.CaptionRevision > lastShownRevision)
        {
            captionText.text = session.Caption;
            lastShownRevision = session.CaptionRevision;
            page = 1; captionText.pageToDisplay = 1;
            captionText.ForceMeshUpdate();
            StartCoroutine(LogCaptionFrame(captureEnded, session.TranscriptionSeconds, session.TranslationSeconds));
        }
        // A previous caption stays visible but is explicitly identified during the next turn.
        if (session.CaptionIsPrevious) statusText.text += "  |  Previous translation";
        UpdateHint();
    }

    private IEnumerator LogCaptionFrame(long ended, double transcriptionSeconds, double translationSeconds)
    {
        yield return new WaitForEndOfFrame();
        double elapsed = (Stopwatch.GetTimestamp() - ended) / (double)Stopwatch.Frequency;
        Debug.Log("[FP2] Caption ready capture_to_caption_frame_s=" + elapsed.ToString("F2") +
            " stt_s=" + transcriptionSeconds.ToString("F2") + " translation_s=" + translationSeconds.ToString("F2"));
    }

    private void UpdateHint()
    {
        int pages = captionText == null ? 1 : Math.Max(1, captionText.textInfo.pageCount);
        hintText.text = "Hold RIGHT trigger: listen   |   Release: translate   |   LEFT trigger: cancel\n" +
            "X: panel height   |   A: next page" + (pages > 1 ? " (" + page + "/" + pages + ")" : "");
    }

    private void OnApplicationPause(bool paused) { if (paused) CancelCurrentTurn(); }
    private void OnDisable() { CancelCurrentTurn(); }
    private void OnDestroy()
    {
        if (session == null) return;
        session.Changed -= RefreshView;
        session.Dispose(); session = null;
    }
}

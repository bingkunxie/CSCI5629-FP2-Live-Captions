using System;
using System.Collections.Generic;
using System.Linq;
using Meta.XR;
using Oculus.Interaction.PoseDetection;
using UnityEditor;
using UnityEngine;

/// <summary>Explicitly invoked by FP2Setup; never changes a scene on import.</summary>
public static class ThumbsUpEffectsSetup
{
    public static ThumbsUpEffects Configure(GameObject owner, Transform eye)
    {
        if (owner == null || eye == null) throw new ArgumentNullException("Gesture owner and eye are required.");
        var caption = owner.GetComponent<QuestLiveCaptions>();
        if (caption == null) throw new InvalidOperationException("Gesture owner must contain QuestLiveCaptions.");
        var panel = new SerializedObject(caption).FindProperty("panel").objectReferenceValue as RectTransform;
        if (panel == null) throw new InvalidOperationException("Caption panel must be configured before gestures.");
        var effects = GetOrAdd<ThumbsUpEffects>(owner);
        var left = FindProvider(owner, "Left");
        var right = FindProvider(owner, "Right");
        var shapes = new[] { Load<ShapeRecognizer>("FingersAllClosed"), Load<ShapeRecognizer>("ThumbUp") };
        var thresholds = Load<TransformFeatureStateThresholds>("DefaultTransformFeatureStateThresholds");
        ConfigureHand(owner, "Left", left, shapes, thresholds, out var leftShape, out var leftOrientation);
        ConfigureHand(owner, "Right", right, shapes, thresholds, out var rightShape, out var rightOrientation);

        // A dedicated disabled camera ensures the default never starts a capture stream.
        var cameraObject = Child(owner.transform, "Thumbs Up Camera");
        cameraObject.SetActive(false);
        var camera = GetOrAdd<PassthroughCameraAccess>(cameraObject);
        camera.enabled = false;
        camera.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
        camera.RequestedResolution = new Vector2Int(1280, 960);
        camera.MaxFramerate = 5;
        cameraObject.SetActive(true);

        var root = panel.Find("Thumbs Up Effect") as RectTransform;
        if (root == null)
        {
            var icon = new GameObject("Thumbs Up Effect", typeof(RectTransform), typeof(CanvasGroup));
            root = icon.GetComponent<RectTransform>();
            root.SetParent(panel, false);
            BuildIcon(root);
        }
        root.anchorMin = root.anchorMax = new Vector2(1, .5f);
        root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(120, 150);
        root.anchoredPosition = new Vector2(100, 10);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        var opacity = GetOrAdd<CanvasGroup>(root.gameObject);
        opacity.alpha = 0;
        opacity.blocksRaycasts = false;
        opacity.interactable = false;

        var settings = new SerializedObject(effects);
        Set(settings, "leftHand", left);
        Set(settings, "rightHand", right);
        Set(settings, "leftShape", leftShape);
        Set(settings, "rightShape", rightShape);
        Set(settings, "leftOrientation", leftOrientation);
        Set(settings, "rightOrientation", rightOrientation);
        Set(settings, "cameraAccess", camera);
        Set(settings, "effectRoot", root);
        Set(settings, "effectOpacity", opacity);
        settings.FindProperty("sampleInterval").floatValue = 3;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(effects);
        return effects;
    }

    private static FingerFeatureStateProvider FindProvider(GameObject owner, string side)
    {
        var candidates = UnityEngine.Object.FindObjectsByType<FingerFeatureStateProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => p.gameObject.scene == owner.scene && HasAncestor(p.transform, "ComprehensiveInteractors" + side)).ToArray();
        if (candidates.Length != 1)
            throw new InvalidOperationException("Expected one " + side + " ComprehensiveInteractors finger provider; found " + candidates.Length + ".");
        if (candidates[0].GetComponent<TransformFeatureStateProvider>() == null)
            throw new InvalidOperationException("Missing " + side + " transform feature provider.");
        return candidates[0];
    }

    private static bool HasAncestor(Transform value, string name)
    {
        while (value != null)
        {
            if (value.name == name) return true;
            value = value.parent;
        }
        return false;
    }

    private static void ConfigureHand(GameObject owner, string side, FingerFeatureStateProvider provider,
        ShapeRecognizer[] shapes, TransformFeatureStateThresholds thresholds,
        out ShapeRecognizerActiveState shape, out TransformRecognizerActiveState orientation)
    {
        var node = Child(owner.transform, side + " Thumbs Up Pose");
        shape = GetOrAdd<ShapeRecognizerActiveState>(node);
        shape.InjectAllShapeRecognizerActiveState(provider, shapes);
        orientation = GetOrAdd<TransformRecognizerActiveState>(node);
        orientation.InjectAllTransformRecognizerActiveState(provider.GetComponent<TransformFeatureStateProvider>(),
            TransformFeatureConfigList.Create(new List<TransformFeatureConfig> {
                new TransformFeatureConfig {
                    Mode = FeatureStateActiveMode.Is,
                    Feature = TransformFeature.WristUp,
                    State = TransformFeatureProperties.FeatureDescriptions[TransformFeature.WristUp].FeatureStates[0].Id
                }
            }), new TransformConfig { UpVectorType = UpVectorType.World, FeatureThresholds = thresholds });
        EditorUtility.SetDirty(shape);
        EditorUtility.SetDirty(orientation);
    }

    // Original geometric artwork: no raster files, fonts, downloads, or custom shaders.
    private static void BuildIcon(RectTransform parent)
    {
        Color gold = new Color32(255, 202, 64, 255);
        Color shadow = new Color32(170, 100, 21, 255);
        Color light = new Color32(255, 231, 151, 255);
        Rect(parent, "Palm outline", new Vector2(5, -7), new Vector2(82, 84), shadow);
        Rect(parent, "Palm", new Vector2(5, -6), new Vector2(74, 76), gold);
        Rect(parent, "Thumb outline", new Vector2(-23, 44), new Vector2(30, 67), shadow, -12);
        Rect(parent, "Thumb", new Vector2(-23, 45), new Vector2(23, 60), gold, -12);
        Rect(parent, "Thumb highlight", new Vector2(-28, 49), new Vector2(5, 38), light, -12);
        for (int i = 0; i < 4; i++)
        {
            Rect(parent, "Finger " + i, new Vector2(21, 21 - i * 19), new Vector2(57 - i * 3, 16), gold);
            Rect(parent, "Finger crease " + i, new Vector2(30, 13 - i * 19), new Vector2(31 - i * 3, 2), shadow);
        }
        Rect(parent, "Cuff outline", new Vector2(-37, -14), new Vector2(24, 67), new Color32(30, 73, 128, 255));
        Rect(parent, "Blue cuff", new Vector2(-37, -13), new Vector2(17, 59), new Color32(87, 170, 244, 255));
        Rect(parent, "Cuff button", new Vector2(-37, -32), new Vector2(6, 6), Color.white);
        Rect(parent, "Spark left", new Vector2(-58, 54), new Vector2(5, 19), light, 35);
        Rect(parent, "Spark right", new Vector2(52, 55), new Vector2(5, 21), light, -38);
        Rect(parent, "Spark top", new Vector2(7, 78), new Vector2(5, 16), light, -5);
    }

    private static void Rect(Transform parent, string name, Vector2 position, Vector2 size, Color color, float angle = 0)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static GameObject Child(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject owner) where T : Component
    {
        T component = owner.GetComponent<T>();
        if (component == null) component = owner.AddComponent<T>();
        return component;
    }
    private static void Set(SerializedObject settings, string name, UnityEngine.Object value) => settings.FindProperty(name).objectReferenceValue = value;

    private static T Load<T>(string name) where T : UnityEngine.Object
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:" + typeof(T).Name, new[] { "Packages/com.meta.xr.sdk.interaction" }))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == name) return asset;
        }
        throw new InvalidOperationException("Missing installed Meta SDK pose asset: " + name);
    }
}

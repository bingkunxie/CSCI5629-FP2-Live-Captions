using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meta.XR.BuildingBlocks;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public static class FP2Setup
{
    public static bool UsbDevelopmentBuild;
    public const string ScenePath = "Assets/Scenes/FP2LiveCaptions.unity";
    public const string PackageId = "edu.umn.csci5629.livecaptions";

    [MenuItem("Tools/FP2/Create Live Caption Scene")]
    public static void CreateScene()
    {
        if (!File.Exists(ScenePath)) throw new InvalidOperationException("Final-project caption scene is missing.");
        UpdateLayout();
        Validate();
    }

    private static void UpdateLayout()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var captions = UnityEngine.Object.FindAnyObjectByType<QuestLiveCaptions>();
        var serialized = new SerializedObject(captions);
        ((RectTransform)serialized.FindProperty("panel").objectReferenceValue).sizeDelta = new Vector2(1000, 380);
        var status = (TMP_Text)serialized.FindProperty("statusText").objectReferenceValue;
        status.rectTransform.sizeDelta = new Vector2(944, 69);
        var hint = (TMP_Text)serialized.FindProperty("hintText").objectReferenceValue;
        hint.rectTransform.anchoredPosition = new Vector2(0, -312);
        hint.rectTransform.sizeDelta = new Vector2(944, 58);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    public static void PrepareBatch() => RunBatch(CreateScene);
    public static void BuildBatch() => RunBatch(Build);

    private static void RunBatch(Action action)
    {
        int code = 0;
        try { action(); }
        catch (Exception error) { Debug.LogException(error); code = 1; }
        // All scene/build operations above are synchronous and explicitly validated.
        // Exit explicitly instead of waiting for unrelated package background operations.
        EditorApplication.Exit(code);
    }

    private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Vector2 topLeft, Vector2 bottomRight)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(1000 + bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
        rect.anchoredPosition = new Vector2((topLeft.x + bottomRight.x) * .5f, topLeft.y);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white; text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false; text.richText = false;
        return text;
    }

    private static void Bind(ControllerButtonsMapper mapper, InputActionAsset asset, InputActionMap map,
        string name, string controller, string keyboard, string interaction, UnityAction callback)
    {
        var action = map.AddAction(name, InputActionType.Button, controller, interactions: interaction);
        action.AddBinding(keyboard, interactions: interaction);
        var reference = InputActionReference.Create(action);
        AssetDatabase.AddObjectToAsset(reference, asset);
        var signal = new UnityEvent(); UnityEventTools.AddPersistentListener(signal, callback);
        mapper.ButtonClickActions.Add(new ControllerButtonsMapper.ButtonClickAction {
            Title = name, Button = OVRInput.Button.None, InputActionReference = reference, Callback = signal
        });
    }

    [MenuItem("Tools/FP2/Validate Live Caption Scene")]
    public static void Validate()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var caption = UnityEngine.Object.FindFirstObjectByType<QuestLiveCaptions>();
        if (caption == null) throw new InvalidOperationException("Missing live caption controller.");
        if (UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(c => c != null && c.GetType().Name == "A3TalkingCube"))
            throw new InvalidOperationException("A3 chatbot still present.");
        var settings = new SerializedObject(caption);
        foreach (string field in new[] { "captionText", "statusText", "hintText", "panel" })
            if (settings.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing " + field);
        var mapper = UnityEngine.Object.FindFirstObjectByType<ControllerButtonsMapper>();
        if (mapper == null || mapper.ButtonClickActions.Count != 5) throw new InvalidOperationException("Missing controller mappings.");
        foreach (var action in mapper.ButtonClickActions)
            if (action.InputActionReference == null || action.Callback.GetPersistentEventCount() != 1 || action.Callback.GetPersistentTarget(0) != caption)
                throw new InvalidOperationException("Broken caption input mapping.");
        Debug.Log("[FP2] PASS: caption scene, five controller callbacks, and view references.");
    }

    public static void Build()
    {
        Validate(); Directory.CreateDirectory("build/FP2");
        string previousId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        string previousName = PlayerSettings.productName;
        InsecureHttpOption previousHttp = PlayerSettings.insecureHttpOption;
        UnityEditor.Build.Reporting.BuildReport report;
        try
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageId);
            PlayerSettings.productName = "FP2 Live Captions";
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            UsbDevelopmentBuild = true;
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "build/FP2/FP2-Live-Captions.apk", options = BuildOptions.Development
            });
        }
        finally
        {
            UsbDevelopmentBuild = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, previousId);
            PlayerSettings.productName = previousName; PlayerSettings.insecureHttpOption = previousHttp;
            AssetDatabase.SaveAssets();
        }
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("FP2 Android build failed: " + report.summary.result);
        Debug.Log("[FP2] Android build succeeded.");
    }
}

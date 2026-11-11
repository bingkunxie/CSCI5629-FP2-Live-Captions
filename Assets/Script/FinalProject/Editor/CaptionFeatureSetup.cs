using System;
using System.Linq;
using Meta.XR.BuildingBlocks;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;

public static class CaptionFeatureSetup
{
    public static void UpgradeBatch()
    {
        try { Upgrade(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/FP2/Configure Voice Language and Gestures")]
    public static void Upgrade()
    {
        var scene = EditorSceneManager.OpenScene(FP2Setup.ScenePath);
        var owner = UnityEngine.Object.FindAnyObjectByType<QuestLiveCaptions>();
        var fields = new SerializedObject(owner);
        var panel = (RectTransform)fields.FindProperty("panel").objectReferenceValue;
        var caption = (TMP_Text)fields.FindProperty("captionText").objectReferenceValue;
        var status = (TMP_Text)fields.FindProperty("statusText").objectReferenceValue;
        var hint = (TMP_Text)fields.FindProperty("hintText").objectReferenceValue;
        var language = panel.Find("LanguageLabel").GetComponent<TMP_Text>();
        var modeNode = panel.Find("ModeStatus");
        if (modeNode == null)
        {
            var go = new GameObject("ModeStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel, false); modeNode = go.transform;
        }
        var modes = modeNode.GetComponent<TextMeshProUGUI>();
        modes.font = language.font; modes.fontSize = 19; modes.enableAutoSizing = false;
        modes.richText = false; modes.raycastTarget = false; modes.color = new Color(.75f, .90f, .85f);
        modes.textWrappingMode = TextWrappingModes.Normal;
        modes.text = "Voice: OFF | Hands: waiting for tracking | Camera off";
        panel.sizeDelta = new Vector2(1000, 440);
        Layout(caption.rectTransform, -64, 141); caption.fontSize = 40;
        Layout(status.rectTransform, -211, 60); status.fontSize = 23;
        Layout(modes.rectTransform, -278, 45);
        Layout(hint.rectTransform, -330, 104); hint.fontSize = 19;
        hint.text = "Hold RIGHT trigger: listen | Release: translate | LEFT trigger: cancel\nB: voice | Y: language | X: height | A: next page\nLEFT grip: camera gestures | Put controllers down for hand tracking";
        caption.font = GetChineseFont();
        var audio = owner.GetComponent<AudioSource>();
        if (audio == null) audio = owner.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = 0; audio.volume = 1;
        var effects = ThumbsUpEffectsSetup.Configure(owner.gameObject, panel.parent);
        fields.FindProperty("languageLabel").objectReferenceValue = language;
        fields.FindProperty("modeText").objectReferenceValue = modes;
        fields.FindProperty("voiceOutput").objectReferenceValue = audio;
        fields.FindProperty("gestures").objectReferenceValue = effects;
        fields.ApplyModifiedPropertiesWithoutUndo();
        var mapper = UnityEngine.Object.FindAnyObjectByType<ControllerButtonsMapper>();
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Script/FinalProject/CaptionInput.asset");
        var map = input.FindActionMap("LiveCaptions", true);
        Bind(mapper, input, map, "Voice", "<XRController>{RightHand}/secondaryButton", "<Keyboard>/v", owner.ToggleVoice);
        Bind(mapper, input, map, "Language", "<XRController>{LeftHand}/secondaryButton", "<Keyboard>/l", owner.ToggleDirection);
        Bind(mapper, input, map, "CameraGestures", "<XRController>{LeftHand}/grip", "<Keyboard>/g", owner.ToggleCameraGestures);
        var manager = UnityEngine.Object.FindAnyObjectByType<OVRManager>();
        var managerSettings = new SerializedObject(manager);
        managerSettings.FindProperty("requestPassthroughCameraAccessPermissionOnStartup").boolValue = false;
        managerSettings.ApplyModifiedPropertiesWithoutUndo();
        var config = OVRProjectConfig.CachedProjectConfig;
        config.isPassthroughCameraAccessEnabled = true; EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(input); EditorUtility.SetDirty(mapper); EditorUtility.SetDirty(owner);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mapper);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[FP2] Voice, direction, Chinese font, and gesture wiring configured.");
    }

    private static void Layout(RectTransform rect, float top, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, top); rect.sizeDelta = new Vector2(944, height);
    }

    private static TMP_FontAsset GetChineseFont()
    {
        const string path = "Assets/Fonts/NotoSansSC/NotoSansCJKsc-SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null && font.sourceFontFile != null && font.material != null && font.atlasTexture != null) return font;
        if (font != null) AssetDatabase.DeleteAsset(path);
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf");
        if (source == null) throw new InvalidOperationException("CJK source font did not import.");
        font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        font.name = "NotoSansCJKsc SDF";
        AssetDatabase.CreateAsset(font, path);
        if (!font.TryAddCharacters("中文翻译将显示在这里。会议下午四点开始你好图书馆", out string missing))
            throw new InvalidOperationException("CJK font is missing required test glyphs: " + missing);
        foreach (Texture2D atlas in font.atlasTextures)
        { atlas.name = "NotoSansCJKsc Atlas"; atlas.hideFlags = HideFlags.None; AssetDatabase.AddObjectToAsset(atlas, font); }
        font.material.hideFlags = HideFlags.None; AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    private static void Bind(ControllerButtonsMapper mapper, InputActionAsset asset, InputActionMap map,
        string name, string binding, string keyboard, UnityAction callback)
    {
        var action = map.FindAction(name) ?? map.AddAction(name, InputActionType.Button, binding, interactions: "press(behavior=0)");
        if (!action.bindings.Any(b => b.path == keyboard)) action.AddBinding(keyboard, interactions: "press(behavior=0)");
        var reference = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)).OfType<InputActionReference>().FirstOrDefault(r => r.action == action);
        if (reference == null) { reference = InputActionReference.Create(action); AssetDatabase.AddObjectToAsset(reference, asset); }
        mapper.ButtonClickActions.RemoveAll(a => a.Title == name);
        var signal = new UnityEvent(); UnityEventTools.AddPersistentListener(signal, callback);
        mapper.ButtonClickActions.Add(new ControllerButtonsMapper.ButtonClickAction {
            Title = name, Button = OVRInput.Button.None, InputActionReference = reference, Callback = signal
        });
    }
}

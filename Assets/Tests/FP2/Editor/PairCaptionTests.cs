using FP2.Captions;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PairCaptionTests
{
    [TestCase(true, "speaking", "A 正在说话，请稍等。")]
    [TestCase(false, "speaking", "A is speaking. Please wait.")]
    [TestCase(true, "processing", "A 正在翻译，请稍等。")]
    [TestCase(false, "reviewing", "A is reviewing the translation. Please wait.")]
    public void PeerWaitUsesReceiverSourceLanguageAndBlocksCapture(bool chinese, string phase, string expected)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FP2LiveCaptions.unity");
        var type = Type.GetType("QuestLiveCaptions, Assembly-CSharp", true);
        var caption = UnityEngine.Object.FindFirstObjectByType(type);
        var fields = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => type.GetField(name, fields).SetValue(caption, value);
        Set("pairRequired", true);
        Set("pairOnline", true);
        Set("pairReady", true);
        Set("pairOwner", "A");
        Set("pairRemotePhase", phase);
        Set("direction", chinese ? TranslationDirection.MandarinToEnglish : TranslationDirection.EnglishToMandarin);
        var configType = Type.GetType("PairConfig, Assembly-CSharp", true);
        var config = Activator.CreateInstance(configType);
        configType.GetField("role").SetValue(config, "B");
        Set("pairConfig", config);
        Assert.That(type.GetMethod("RequestPairTurn", fields).Invoke(caption, null), Is.True,
            "The local recording path must be consumed while another speaker owns the turn.");
        var status = type.GetField("statusText", fields).GetValue(caption);
        Assert.That(status.GetType().GetProperty("text").GetValue(status), Is.EqualTo(expected));
        Assert.That(type.GetField("pairTurn", fields).GetValue(caption), Is.EqualTo(""));
        Assert.That(type.GetField("recording", fields).GetValue(caption), Is.Null);
    }

    [Test]
    public void DisconnectedPairCannotFallBackToUncoordinatedRecording()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FP2LiveCaptions.unity");
        var type = Type.GetType("QuestLiveCaptions, Assembly-CSharp", true);
        var caption = UnityEngine.Object.FindFirstObjectByType(type);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("pairRequired", flags).SetValue(caption, true);
        Assert.That(type.GetMethod("RequestPairTurn", flags).Invoke(caption, null), Is.True);
        Assert.That(type.GetField("pairHeld", flags).GetValue(caption), Is.False);
    }
}

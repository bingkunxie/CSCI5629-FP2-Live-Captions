using System;
using System.Reflection;
using FP2.Captions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
public class UiLanguageTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void MainPanelToolsAndHintsFollowSourceNotTranslationLanguage(bool chinese)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FP2LiveCaptions.unity");
        var type=Type.GetType("QuestLiveCaptions, Assembly-CSharp",true);
        var host=UnityEngine.Object.FindFirstObjectByType(type);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        type.GetField("direction",flags).SetValue(host,chinese?TranslationDirection.MandarinToEnglish:TranslationDirection.EnglishToMandarin);
        void Call(string method)=>type.GetMethod(method,flags).Invoke(host,null);
        string Read(string field)
        {
            var label=type.GetField(field,flags).GetValue(host);
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }
        try
        {
            Call("ShowDirection");Call("UpdateHint");
            Assert.That(Read("languageLabel"),Is.EqualTo(chinese?"普通话 → 英语":"English → Mandarin"));
            Assert.That(Read("captionText"),Is.EqualTo(chinese?"英文译文将显示在这里。":"Mandarin translations will appear here."));
            Assert.That(Read("hintText"),Does.Contain(chinese?"原文语言":"language"));
            Assert.That(Read("modeText"),Does.Contain(chinese?"朗读：":"Voice: "));
            Assert.That(Read("modeText"),Does.Not.Contain(chinese?"OFF":"关闭"));
            Call("DrawTools");
            Assert.That(Read("captionText"),Does.Contain(chinese?"朗读口音":"Voice accent"));
            Assert.That(Read("captionText"),Does.Contain(chinese?"美式英语":"Mainland Mandarin"));
            Call("DrawHistory");
            Assert.That(Read("captionText"),Is.EqualTo(chinese?"记事本暂无对话。":"Your notebook is empty."));
        }
        finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
    }
    [Test] public void CaptureFailuresAndCombinedGestureStatusAreLocalized()
    {
        Assert.That(UiText.Get("Cannot reach the gateway. Check campus Wi-Fi or the USB/VPN route.",true),
            Is.EqualTo("无法连接网关，请检查校园网或电脑中继连接。"));
        Assert.That(UiText.Get("Recognizing English…",true),Is.EqualTo("正在识别英语…"));
        Assert.That(UiText.Status("Hands: tracked | Camera off",true),Is.EqualTo("手部：已追踪 | 摄像头关闭"));
        Assert.That(UiText.Get("请重新连接后保存共享纲要。",false),Is.EqualTo("Reconnect to save the shared agenda."));
    }
}

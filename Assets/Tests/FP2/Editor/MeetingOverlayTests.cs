using System;
using System.Reflection;
using FP2.Captions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public class MeetingOverlayTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private Type type;
    private InputTestFixture inputFixture;
    private Component ui;
    private UnityEngine.Object host;
    [SetUp] public void SetUp()
    {
        inputFixture=new InputTestFixture();inputFixture.Setup();
        EditorSceneManager.OpenScene("Assets/Scenes/FP2LiveCaptions.unity");
        var hostType=Type.GetType("QuestLiveCaptions, Assembly-CSharp",true);
        host=UnityEngine.Object.FindFirstObjectByType(hostType);
        var panel=(RectTransform)hostType.GetField("panel",Hidden).GetValue(host);
        var caption=hostType.GetField("captionText",Hidden).GetValue(host);
        var font=caption.GetType().GetProperty("font").GetValue(caption);
        type=Type.GetType("MeetingOverlay, Assembly-CSharp",true);
        ui=panel.gameObject.AddComponent(type);
        Call("Configure",panel,font,null);
        hostType.GetField("meetingUI",Hidden).SetValue(host,ui);
    }
    private object Call(string name,params object[] args)=>type.GetMethod(name,BindingFlags.Public|Hidden).Invoke(ui,args);
    private RectTransform Rect(string name)=>(RectTransform)type.GetField(name,Hidden).GetValue(ui);
    [TearDown] public void TearDown()
    {
        try
        {
            if(ui!=null)UnityEngine.Object.DestroyImmediate(ui);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
        finally {inputFixture?.TearDown();inputFixture=null;}
    }
    [Test] public void AgendaEditorConsumesRecordingTriggerWithoutGatewayOrMicrophone()
    {
        Call("OpenEditor");
        host.GetType().GetMethod("BeginListening").Invoke(host,null);
        Assert.That(host.GetType().GetField("session",Hidden).GetValue(host),Is.Null);
        Assert.That(host.GetType().GetField("recording",Hidden).GetValue(host),Is.Null);
        Assert.That(type.GetProperty("ModalOpen").GetValue(ui),Is.True);
    }
    [Test] public void PaletteCollapsesAndPopupExpiresWithoutChangingCaption()
    {
        Call("TogglePalette");Assert.That(Rect("palette").gameObject.activeSelf,Is.True);
        Call("TogglePalette");Assert.That(Rect("palette").gameObject.activeSelf,Is.False);
        Call("ShowReaction","A","like",false);Assert.That(Rect("toast").gameObject.activeSelf,Is.True);
        type.GetField("toastUntil",Hidden).SetValue(ui,0d);Call("Update");
        Assert.That(Rect("toast").gameObject.activeSelf,Is.False);
        Assert.That(host.GetType().GetField("session",Hidden).GetValue(host),Is.Null);
    }
    [Test] public void RemoteUpdateDoesNotSilentlyOverwriteAnOpenDraft()
    {
        var original=MeetingAgenda.Replace(MeetingAgenda.Empty(),new[]{"First point","第二项"});
        Call("SetAgenda",original);Call("OpenEditor");
        Call("SetAgenda",MeetingAgenda.Replace(original,new[]{"Remote replacement"}));
        Assert.That(type.GetField("draftVersion",Hidden).GetValue(ui),Is.EqualTo(original.agendaVersion));
        var draft=(System.Collections.Generic.List<string>)type.GetField("draft",Hidden).GetValue(ui);
        Assert.That(draft,Is.EqualTo(new[]{"First point","第二项"}));
    }
    [Test] public void CheckboxEmitsItemIdentityAndExplicitCompletion()
    {
        var agenda=MeetingAgenda.Replace(MeetingAgenda.Empty(),new[]{"Budget","Schedule"});
        Call("SetAgenda",agenda);
        string id=null;bool done=false;
        Action<string,bool> handler=(key,value)=>{id=key;done=value;};
        type.GetEvent("AgendaToggled").AddEventHandler(ui,handler);
        Call("SelectRow",1);
        Assert.That(id,Is.EqualTo(agenda.items[1].id));Assert.That(done,Is.True);
        Assert.That(agenda.items[0].done,Is.False);
    }

    private string Label(string objectName)
    {
        var choiceList=(System.Collections.IEnumerable)type.GetField("choices",Hidden).GetValue(ui);
        foreach(var choice in choiceList)
        {
            var rect=(RectTransform)choice.GetType().GetField("rect").GetValue(choice);
            if(rect.name!=objectName)continue;
            var label=choice.GetType().GetField("label").GetValue(choice);
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }
        throw new Exception("Missing button "+objectName);
    }
    private string TextField(string name)
    {
        var label=type.GetField(name,Hidden).GetValue(ui);
        return (string)label.GetType().GetProperty("text").GetValue(label);
    }
    [Test] public void SourceLanguageSwitchRefreshesEveryEditorButtonWithoutChangingDraft()
    {
        var data=MeetingAgenda.Replace(MeetingAgenda.Empty(),new[]{"Keep my English text","保留中文内容"});
        Call("SetAgenda",data);Call("OpenEditor");Call("SetLanguage",true);
        Assert.That(Label("Save agenda"),Is.EqualTo("保存"));
        Assert.That(Label("Cancel edit"),Is.EqualTo("取消"));
        Assert.That(Label("System keyboard"),Is.EqualTo("系统键盘"));
        Assert.That(Label("Previous item"),Is.EqualTo("上一项"));
        Assert.That(TextField("draftText"),Does.Contain("最多 6 项").And.Contain("Keep my English text"));
        Call("SetLanguage",false);
        Assert.That(Label("Save agenda"),Is.EqualTo("Save"));
        Assert.That(Label("System keyboard"),Is.EqualTo("System keyboard"));
        Assert.That(TextField("draftText"),Does.Contain("max 6 items").And.Contain("Keep my English text"));
        var draft=(System.Collections.Generic.List<string>)type.GetField("draft",Hidden).GetValue(ui);
        Assert.That(draft,Is.EqualTo(new[]{"Keep my English text","保留中文内容"}));
    }
    [Test] public void ExistingNoticeAndToastChangeLanguageWithoutExtendingPopupLifetime()
    {
        Call("SetStatus","Reconnect to save the shared agenda.");
        Call("ShowReaction","You","like",true);
        var expiry=type.GetField("toastUntil",Hidden).GetValue(ui);
        Call("SetLanguage",true);
        Assert.That(TextField("message"),Is.EqualTo("请重新连接后保存共享纲要。"));
        Assert.That(TextField("toastText"),Is.EqualTo("你\n点赞\n本机预览"));
        Call("SetLanguage",false);
        Assert.That(TextField("message"),Is.EqualTo("Reconnect to save the shared agenda."));
        Assert.That(TextField("toastText"),Is.EqualTo("You\nLike\nLocal preview"));
        Assert.That(type.GetField("toastUntil",Hidden).GetValue(ui),Is.EqualTo(expiry));
    }

    [TestCase("OculusTouchControllerProfile+OculusTouchController")]
    [TestCase("MetaQuestTouchPlusControllerProfile+QuestTouchPlusController")]
    public void ActualOpenXRThumbstickBindingOpensOncePerPressAndKeepsHandsSeparate(string layoutClass)
    {
        var deviceType=Type.GetType("UnityEngine.XR.OpenXR.Features.Interactions."+layoutClass+", Unity.XR.OpenXR",true);
        InputSystem.RegisterLayout(Type.GetType("UnityEngine.XR.OpenXR.Input.HapticControl, Unity.XR.OpenXR",true),"Haptic");
        InputSystem.RegisterLayout(deviceType.GetProperty("devicePose").PropertyType,"Pose");
        InputSystem.RegisterLayout(deviceType);
        var right=InputSystem.AddDevice(deviceType.Name);
        var left=InputSystem.AddDevice(deviceType.Name);
        InputSystem.SetDeviceUsage(right,"RightHand");InputSystem.SetDeviceUsage(left,"LeftHand");
        void Set(UnityEngine.InputSystem.InputDevice device,string control,float value)
        {
            using(StateEvent.From(device,out var e))
            {
                device.GetChildControl<ButtonControl>(control).WriteValueIntoEvent(value,e);
                InputSystem.QueueEvent(e);
            }
            InputSystem.Update();Call("PollMenuButtons");
        }
        try
        {
            using(var old=new InputAction(binding:"<XRController>{RightHand}/primary2DAxisClick"))
            {
                old.Enable();Assert.That(old.controls.Count,Is.Zero,"Reproduces the previous invalid control-name binding.");
            }
            var action=(InputAction)type.GetField("reactionMenuAction",Hidden).GetValue(ui);
            Assert.That(action.controls.Count,Is.GreaterThan(0),"Corrected path must resolve a right-hand control.");
            Call("PollMenuButtons");
            Set(right,"primaryButton",1);Set(right,"thumbstickTouched",1);
            Assert.That(Rect("palette").gameObject.activeSelf,Is.False,"A and stick touch must not open reactions.");
            Set(right,"thumbstickClicked",1);
            Assert.That(right.GetChildControl<ButtonControl>("thumbstickClicked").isPressed,Is.True,"Simulated runtime press must reach the controller state.");
            Assert.That(action.IsPressed(),Is.True,"Resolved action must read the pressed state.");
            Assert.That(Rect("palette").gameObject.activeSelf,Is.True);
            for(int i=0;i<4;i++)Call("PollMenuButtons");
            Assert.That(Rect("palette").gameObject.activeSelf,Is.True,"Holding must not repeatedly toggle.");
            Set(right,"thumbstickClicked",0);Set(right,"thumbstickClicked",1);
            Assert.That(Rect("palette").gameObject.activeSelf,Is.False,"Next distinct press closes.");
            Set(right,"thumbstickClicked",0);Set(left,"thumbstickClicked",1);
            Assert.That(Rect("editor").gameObject.activeSelf,Is.True);
            Assert.That(Rect("palette").gameObject.activeSelf,Is.False,"Left stick opens agenda, not reactions.");
        }
        finally {InputSystem.RemoveDevice(right);InputSystem.RemoveDevice(left);}
    }
    [Test] public void ResumeRequiresReleaseAndMergedHeldInputDoesNotDoubleToggle()
    {
        Call("ApplyMenuButtons",true,false);
        Assert.That(Rect("palette").gameObject.activeSelf,Is.False,"Ignore a stick already held at startup/resume.");
        Call("ApplyMenuButtons",false,false);Call("ApplyMenuButtons",true,false);
        Assert.That(Rect("palette").gameObject.activeSelf,Is.True);
        Call("ApplyMenuButtons",true,false);Call("ApplyMenuButtons",true,false);
        Assert.That(Rect("palette").gameObject.activeSelf,Is.True);
        Call("OnDisable");Call("OnEnable");Call("ApplyMenuButtons",true,false);
        Assert.That(Rect("palette").gameObject.activeSelf,Is.True,"Resume must not create a new press.");
    }
}

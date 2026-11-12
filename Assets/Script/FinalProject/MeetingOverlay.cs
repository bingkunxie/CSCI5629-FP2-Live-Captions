using System;
using System.Collections.Generic;
using System.Linq;
using FP2.Captions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

public sealed class MeetingOverlay : MonoBehaviour
{
    private sealed class Choice { public RectTransform rect; public Image image; public TMP_Text label; public Action click; }
    private readonly List<Choice> choices=new List<Choice>();
    private readonly List<Choice> rows=new List<Choice>();
    private RectTransform anchor, palette, editor, toast;
    private TMP_FontAsset font;
    private Transform tracking;
    private TMP_Text title, message, draftText, toastText;
    private ReactionGraphic toastIcon;
    private CanvasGroup toastGroup;
    private Image cursor;
    private Choice hover, gestureButton;
    private AgendaSnapshot agenda=MeetingAgenda.Empty();
    private readonly List<string> draft=new List<string>();
    private int draftIndex, draftVersion;
    private bool chinese, shift;
    private double toastUntil;
    private TouchScreenKeyboard keyboard;
    private InputAction reactionMenuAction, agendaEditAction;
    public bool GestureShortcut=true;
    public bool ModalOpen => palette != null && (palette.gameObject.activeSelf || editor.gameObject.activeSelf);
    public event Action<string> ReactionChosen;
    public event Action<string[],int> AgendaSaved;
    public event Action<string,bool> AgendaToggled;
    public event Action ModalOpened;
    private static double Now => System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;

    public void Configure(RectTransform parent,TMP_FontAsset textFont,Transform trackingSpace)
    {
        anchor=parent;font=textFont;tracking=trackingSpace;
        var board=Panel("Meeting agenda",anchor,new Vector2(800,0),new Vector2(560,690),new Color(.035f,.10f,.12f,.78f));
        title=Text(board,"Agenda title","MEETING AGENDA",new Vector2(0,-15),new Vector2(520,42),27);
        for(int i=0;i<6;i++)
        {
            int index=i;
            var row=Button(board,"Agenda "+i,"",new Vector2(0,-67-i*82),new Vector2(526,76),()=>SelectRow(index));
            row.label.fontSize=21;row.label.alignment=TextAlignmentOptions.MidlineLeft;
            rows.Add(row);
        }
        Button(board,"Edit agenda","Edit agenda",new Vector2(-132,-570),new Vector2(250,48),OpenEditor);
        Button(board,"Reactions","Reactions",new Vector2(132,-570),new Vector2(250,48),TogglePalette);
        message=Text(board,"Meeting status","",new Vector2(0,-627),new Vector2(520,55),18);
        // Always-visible compact toggle, with a translucent collapsible palette.
        Button(anchor,"Reactions toggle","Reactions +",new Vector2(360,72),new Vector2(230,52),TogglePalette);
        palette=Panel("Reaction palette",anchor,new Vector2(0,245),new Vector2(900,166),new Color(.05f,.14f,.18f,.82f));
        for(int i=0;i<ReactionGate.Kinds.Length;i++)
        {
            string kind=ReactionGate.Kinds[i];
            var button=Button(palette,"Send "+kind,"",new Vector2(-330+i*220,-12),new Vector2(205,106),()=>{ReactionChosen?.Invoke(kind);palette.gameObject.SetActive(false);});
            var icon=NewRect("Reaction "+kind,button.rect,new Vector2(0,-4),new Vector2(62,62));
            icon.pivot=new Vector2(.5f,1);
            icon.gameObject.AddComponent<ReactionGraphic>().SetKind(kind);
            button.label.text=ReactionName(kind);button.label.fontSize=20;
            button.label.rectTransform.anchoredPosition=new Vector2(0,-70);button.label.rectTransform.sizeDelta=new Vector2(195,30);
        }
        gestureButton=Button(palette,"Gesture shortcut","Thumbs-up shortcut: ON",new Vector2(0,-122),new Vector2(510,35),
            ()=>{GestureShortcut=!GestureShortcut;RefreshLanguage();});
        palette.gameObject.SetActive(false);
        BuildEditor();
        toast=Panel("Reaction toast",anchor,new Vector2(0,410),new Vector2(460,145),new Color(.03f,.12f,.15f,.94f));
        toastGroup=toast.gameObject.AddComponent<CanvasGroup>();toastGroup.blocksRaycasts=false;
        var graphic=NewRect("Toast artwork",toast,new Vector2(-155,-22),new Vector2(95,95));
        graphic.pivot=new Vector2(.5f,1);toastIcon=graphic.gameObject.AddComponent<ReactionGraphic>();
        toastText=Text(toast,"Toast sender","",new Vector2(65,-28),new Vector2(295,95),28);
        toast.gameObject.SetActive(false);
        var pointer=NewRect("Controller pointer",anchor,Vector2.zero,new Vector2(12,12));
        pointer.anchorMin=pointer.anchorMax=pointer.pivot=new Vector2(.5f,.5f);
        cursor=pointer.gameObject.AddComponent<Image>();cursor.color=new Color(1,.9f,.35f);cursor.raycastTarget=false;
        reactionMenuAction=new InputAction("Reactions menu",InputActionType.Button,"<XRController>{RightHand}/primary2DAxisClick");
        reactionMenuAction.AddBinding("<Keyboard>/f2");reactionMenuAction.performed+=_=>TogglePalette();reactionMenuAction.Enable();
        agendaEditAction=new InputAction("Agenda editor",InputActionType.Button,"<XRController>{LeftHand}/primary2DAxisClick");
        agendaEditAction.AddBinding("<Keyboard>/f3");agendaEditAction.performed+=_=>OpenEditor();agendaEditAction.Enable();
        DrawAgenda();
    }

    private RectTransform NewRect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();
        r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);
        r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    private RectTransform Panel(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var r=NewRect(name,parent,position,size);var image=r.gameObject.AddComponent<Image>();
        image.color=color;image.raycastTarget=false;return r;
    }
    private TMP_Text Text(Transform parent,string name,string value,Vector2 position,Vector2 size,float fontSize)
    {
        var r=NewRect(name,parent,position,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.fontSize=fontSize;t.text=value;t.color=Color.white;t.richText=false;
        t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.Normal;t.raycastTarget=false;return t;
    }
    private Choice Button(Transform parent,string name,string label,Vector2 position,Vector2 size,Action callback)
    {
        var r=Panel(name,parent,position,size,new Color(.13f,.27f,.30f,.90f));
        var c=new Choice{rect=r,image=r.GetComponent<Image>(),click=callback,
            label=Text(r,name+" label",label,new Vector2(0,-3),size-new Vector2(16,6),22)};
        choices.Add(c);return c;
    }
    public void SetLanguage(bool value){chinese=value;RefreshLanguage();DrawAgenda();}
    private void RefreshLanguage()
    {
        if(title==null)return;
        title.text=chinese?"会议纲要":"MEETING AGENDA";
        if(gestureButton!=null)gestureButton.label.text=(chinese?"点赞手势快捷发送：":"Thumbs-up shortcut: ")+(GestureShortcut?"ON":"OFF");
        foreach(var c in choices)
        {
            if(c.rect.name=="Edit agenda")c.label.text=chinese?"编辑纲要":"Edit agenda";
            if(c.rect.name=="Reactions")c.label.text=chinese?"表情":"Reactions";
            if(c.rect.name=="Reactions toggle")c.label.text=chinese?"表情 +":"Reactions +";
            if(c.rect.name.StartsWith("Send "))c.label.text=ReactionName(c.rect.name.Substring(5));
        }
    }
    private string ReactionName(string kind) => chinese ?
        kind=="like"?"点赞":kind=="clap"?"鼓掌":kind=="love"?"赞同":"疑问" :
        kind=="like"?"Like":kind=="clap"?"Applause":kind=="love"?"Love":"Question";
    public void SetAgenda(AgendaSnapshot value){MeetingAgenda.Validate(value);agenda=value;DrawAgenda();}
    public void SetStatus(string value){if(message!=null)message.text=value;}
    private void DrawAgenda()
    {
        for(int i=0;i<rows.Count;i++)
        {
            bool exists=i<agenda.items.Length;
            rows[i].rect.gameObject.SetActive(exists);
            if(exists)rows[i].label.text=(agenda.items[i].done?"[x]  ":"[ ]  ")+agenda.items[i].text;
        }
        if(message!=null && agenda.items.Length==0)message.text=chinese?"会前点“编辑纲要”添加要点。":"Choose Edit agenda before your meeting.";
    }
    private void SelectRow(int index)
    {
        if(editor.gameObject.activeSelf)
        {
            if(index<draft.Count){draftIndex=index;DrawDraft();}return;
        }
        if(index<agenda.items.Length)AgendaToggled?.Invoke(agenda.items[index].id,!agenda.items[index].done);
    }
    public void TogglePalette()
    {
        if(palette==null)return;
        bool open=!palette.gameObject.activeSelf;
        if(open){ModalOpened?.Invoke();CloseEditor();}
        palette.gameObject.SetActive(open);
    }
    public void ShowReaction(string role,string kind,bool preview=false)
    {
        toastIcon.SetKind(kind);toastText.text=role+"\n"+ReactionName(kind)+(preview?(chinese?"\n本机预览":"\nLocal preview"):"");
        toastUntil=Now+ReactionGate.Lifetime;toastGroup.alpha=1;toast.gameObject.SetActive(true);
    }
    private void BuildEditor()
    {
        editor=Panel("Agenda editor",anchor,Vector2.zero,new Vector2(1000,590),new Color(.025f,.08f,.10f,.98f));
        draftText=Text(editor,"Draft item","",new Vector2(0,-15),new Vector2(910,120),27);
        Button(editor,"Previous item","< Item",new Vector2(-330,-145),new Vector2(170,45),()=>{if(draft.Count>0)draftIndex=(draftIndex+draft.Count-1)%draft.Count;DrawDraft();});
        Button(editor,"Next item","Item >",new Vector2(-140,-145),new Vector2(170,45),()=>{if(draft.Count>0)draftIndex=(draftIndex+1)%draft.Count;DrawDraft();});
        Button(editor,"Add item","+ Item",new Vector2(70,-145),new Vector2(170,45),()=>{if(draft.Count<6){draft.Add("");draftIndex=draft.Count-1;}DrawDraft();});
        Button(editor,"Delete item","Delete item",new Vector2(300,-145),new Vector2(240,45),()=>{if(draft.Count>0)draft.RemoveAt(draftIndex);draftIndex=Math.Max(0,Math.Min(draftIndex,draft.Count-1));DrawDraft();});
        string[] keys={"1234567890","qwertyuiop","asdfghjkl","zxcvbnm.,?"};
        for(int row=0;row<keys.Length;row++)
            for(int col=0;col<keys[row].Length;col++)
            {
                string key=keys[row][col].ToString();
                Button(editor,"Key "+key,key.ToUpperInvariant(),new Vector2((col-(keys[row].Length-1)/2f)*75,-207-row*60),
                    new Vector2(68,51),()=>Append(shift?key.ToUpperInvariant():key));
            }
        Button(editor,"Shift","Shift",new Vector2(-350,-452),new Vector2(150,47),()=>{shift=!shift;SetStatus(shift?"ABC":"abc");});
        Button(editor,"Space","Space",new Vector2(-130,-452),new Vector2(250,47),()=>Append(" "));
        Button(editor,"Backspace","Backspace",new Vector2(115,-452),new Vector2(200,47),Backspace);
        Button(editor,"System keyboard","Keyboard / 中文",new Vector2(350,-452),new Vector2(240,47),OpenSystemKeyboard);
        Button(editor,"Save agenda","Save / 保存",new Vector2(-180,-520),new Vector2(280,50),
            ()=>AgendaSaved?.Invoke(draft.ToArray(),draftVersion));
        Button(editor,"Cancel edit","Cancel / 取消",new Vector2(180,-520),new Vector2(280,50),CloseEditor);
        editor.gameObject.SetActive(false);
    }
    public void OpenEditor()
    {
        if(editor==null)return;
        ModalOpened?.Invoke();palette.gameObject.SetActive(false);
        draft.Clear();draft.AddRange(agenda.items.Select(x=>x.text));if(draft.Count==0)draft.Add("");
        draftIndex=0;draftVersion=agenda.agendaVersion;editor.gameObject.SetActive(true);DrawDraft();
    }
    public void CloseEditor(){if(keyboard!=null)keyboard.active=false;keyboard=null;editor.gameObject.SetActive(false);}
    private void DrawDraft()
    {
        draftText.text=(chinese?"编辑要点 ":"Edit item ")+(draft.Count==0?0:draftIndex+1)+"/"+draft.Count+
            "   (max 6 items / 60 characters)\n"+(draft.Count==0?"":draft[draftIndex])+"|";
    }
    private void Append(string value)
    {
        if(draft.Count==0){draft.Add("");draftIndex=0;}
        if(draft[draftIndex].Length+value.Length<=MeetingAgenda.MaxLength)draft[draftIndex]+=value;
        DrawDraft();
    }
    private void Backspace()
    {
        if(draft.Count>0 && draft[draftIndex].Length>0)
        {
            string text=draft[draftIndex];int count=char.IsLowSurrogate(text[text.Length-1])&&text.Length>1?2:1;
            draft[draftIndex]=text.Substring(0,text.Length-count);
        }
        DrawDraft();
    }
    private void OpenSystemKeyboard()
    {
        if(draft.Count==0){draft.Add("");draftIndex=0;}
        if(TouchScreenKeyboard.isSupported)
            keyboard=TouchScreenKeyboard.Open(draft[draftIndex],TouchScreenKeyboardType.Default,false,false,false,false,"Agenda item",60);
        else SetStatus(chinese?"可用屏幕按键或电脑离线页面输入。":"Use the VR keys or the offline laptop editor.");
    }
    private bool GetRay(out Ray ray)
    {
        ray=default;
#if UNITY_EDITOR
        var camera=Camera.main;
        if(camera!=null&&Mouse.current!=null){ray=camera.ScreenPointToRay(Mouse.current.position.ReadValue());return true;}
#endif
        var device=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if(tracking!=null&&device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 position)&&
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out Quaternion rotation))
        {ray=new Ray(tracking.TransformPoint(position),tracking.TransformDirection(rotation*Vector3.forward));return true;}
        return false;
    }
    private void UpdatePointer()
    {
        hover=null;if(anchor==null)return;
        if(GetRay(out Ray ray))
            for(int i=choices.Count-1;i>=0;i--)
            {
                var c=choices[i];if(!c.rect.gameObject.activeInHierarchy)continue;
                if(new Plane(c.rect.forward,c.rect.position).Raycast(ray,out float distance)&&distance>0&&distance<5)
                {
                    Vector3 hit=ray.GetPoint(distance),local=c.rect.InverseTransformPoint(hit);
                    if(c.rect.rect.Contains(new Vector2(local.x,local.y)))
                    {hover=c;cursor.rectTransform.position=hit-ray.direction*.003f;break;}
                }
            }
        foreach(var c in choices)c.image.color=c==hover?new Color(.28f,.58f,.53f,.98f):new Color(.13f,.27f,.30f,.90f);
        if(cursor!=null){cursor.gameObject.SetActive(hover!=null);cursor.transform.SetAsLastSibling();}
    }
    public bool TryConsumeTrigger()
    {
        UpdatePointer();
        if(hover!=null){hover.click();return true;}
        return ModalOpen; // Never record while operating the palette or editor.
    }
    private void Update()
    {
        UpdatePointer();
        if(toast!=null&&toast.gameObject.activeSelf)
        {
            double remaining=toastUntil-Now;
            if(remaining<=0)toast.gameObject.SetActive(false);
            else toastGroup.alpha=Mathf.Clamp01((float)(remaining/.35));
        }
        if(keyboard!=null && keyboard.status==TouchScreenKeyboard.Status.Done)
        {
            string value=keyboard.text.Replace("\r"," ").Replace("\n"," ");
            if(value.Length<=60)draft[draftIndex]=value;
            keyboard=null;DrawDraft();
        }
#if UNITY_EDITOR
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)TryConsumeTrigger();
#endif
    }
    private void OnEnable(){reactionMenuAction?.Enable();agendaEditAction?.Enable();}
    private void OnDisable(){reactionMenuAction?.Disable();agendaEditAction?.Disable();if(keyboard!=null)keyboard.active=false;}
    private void OnDestroy(){reactionMenuAction?.Dispose();agendaEditAction?.Dispose();}
}

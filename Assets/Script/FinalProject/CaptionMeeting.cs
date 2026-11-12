using System;
using System.IO;
using FP2.Captions;
using UnityEngine;

public sealed partial class QuestLiveCaptions
{
    private MeetingOverlay meetingUI;
    private AgendaSnapshot meetingAgenda;
    private AgendaStore agendaStore;
    private readonly ReactionGate reactionGate=new ReactionGate();
    private string pendingAgendaSave="";
    private static double MeetingNow => System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;

    private void InitializeMeeting()
    {
        meetingAgenda=MeetingAgenda.Empty();
        string error="";
        try
        {
            agendaStore=new AgendaStore(Path.Combine(ConfigurationDirectory,"meeting-agenda.dat"));
            meetingAgenda=agendaStore.Load();
            string import=Path.Combine(ConfigurationDirectory,"meeting-agenda-import.json");
            if(!pairRequired && File.Exists(import))
            {
                var imported=JsonUtility.FromJson<AgendaSnapshot>(File.ReadAllText(import));
                MeetingAgenda.Validate(imported);agendaStore.Save(imported);meetingAgenda=imported;
                File.Copy(import,import+".applied",true);File.Delete(import);
            }
        }
        catch(Exception){agendaStore=null;error="Agenda storage unavailable; existing files retained.";}
        meetingUI=gameObject.AddComponent<MeetingOverlay>();
        var rig=panel.GetComponentInParent<OVRCameraRig>();
        meetingUI.Configure(panel,captionText.font,rig!=null?rig.trackingSpace:panel.parent.parent);
        meetingUI.SetLanguage(PairChinese);meetingUI.SetAgenda(meetingAgenda);
        if(error.Length>0)meetingUI.SetStatus(error);
        meetingUI.ReactionChosen+=SendReaction;
        meetingUI.AgendaSaved+=SaveMeetingDraft;
        meetingUI.AgendaToggled+=SetAgendaDone;
        meetingUI.ModalOpened+=CancelCurrentTurn;
        if(gestures!=null)gestures.NativeThumbsUp+=SendThumbReaction;
    }
    private void SendThumbReaction()
    {
        if(isActiveAndEnabled && meetingUI!=null && meetingUI.GestureShortcut)SendReaction("like");
    }
    private void SendReaction(string kind)
    {
        if(!reactionGate.TrySend(kind,MeetingNow)){meetingUI.SetStatus("Wait a moment before another reaction.");return;}
        string id=Guid.NewGuid().ToString("N");
        if(pairRequired)
        {
            if(!pairOnline||!pairReady){meetingUI.SetStatus("Partner disconnected; reaction not sent.");return;}
            pair.Send(new PairPacket{type="reaction",eventId=id,reaction=kind});
        }
        else meetingUI.ShowReaction("You",kind,true);
    }
    private void SaveMeetingDraft(string[] lines,int version)
    {
        try
        {
            var candidate=MeetingAgenda.Replace(meetingAgenda,lines);
            if(pairRequired)
            {
                if(!pairOnline){meetingUI.SetStatus("Reconnect to save the shared agenda.");return;}
                pendingAgendaSave=Guid.NewGuid().ToString("N");
                pair.Send(new PairPacket{type="agenda_replace",agendaVersion=version,items=candidate.items,eventId=pendingAgendaSave});
                meetingUI.SetStatus("Saving shared agenda…");
            }
            else
            {
                if(agendaStore==null)throw new IOException();
                agendaStore.Save(candidate);meetingAgenda=candidate;meetingUI.SetAgenda(candidate);meetingUI.CloseEditor();
                meetingUI.SetStatus("Agenda saved on this headset.");
            }
        }
        catch(ArgumentException){meetingUI.SetStatus("Use up to 6 items, each at most 60 characters.");}
        catch(Exception){meetingUI.SetStatus("Could not save agenda; existing agenda retained.");}
    }
    private void SetAgendaDone(string id,bool done)
    {
        try
        {
            if(pairRequired)
            {
                if(!pairOnline){meetingUI.SetStatus("Reconnect to update the shared agenda.");return;}
                pair.Send(new PairPacket{type="agenda_set",itemId=id,done=done});
            }
            else
            {
                if(agendaStore==null)throw new IOException();
                var next=MeetingAgenda.SetDone(meetingAgenda,id,done);
                agendaStore.Save(next);meetingAgenda=next;meetingUI.SetAgenda(next);
                meetingUI.SetStatus("Completion saved.");
            }
        }
        catch(Exception){meetingUI.SetStatus("Could not update agenda; previous state retained.");}
    }
    private bool HandleMeetingPacket(PairPacket packet)
    {
        if(meetingUI==null)return false;
        if(packet.type=="reaction")
        {
            if(reactionGate.Accept(packet.eventId,packet.reaction,packet.receivedAt,MeetingNow))
            {
                meetingUI.ShowReaction(packet.owner==pairConfig.role?("You"):packet.owner,packet.reaction);
                Debug.Log("[FP2 Meeting] Reaction shown: "+packet.reaction);
            }
            return true;
        }
        if(packet.type=="agenda")
        {
            try
            {
                var next=new AgendaSnapshot{meetingId=packet.meetingId,agendaVersion=packet.agendaVersion,items=packet.items};
                MeetingAgenda.Validate(next);meetingAgenda=next;meetingUI.SetAgenda(next);
                try {agendaStore?.Save(next);}catch(Exception){meetingUI.SetStatus("Agenda synchronized; local backup unavailable.");}
                if(!string.IsNullOrEmpty(pendingAgendaSave)&&packet.eventId==pendingAgendaSave)
                {pendingAgendaSave="";meetingUI.CloseEditor();meetingUI.SetStatus("Shared agenda saved.");}
                Debug.Log("[FP2 Meeting] Agenda synchronized: "+next.items.Length+" items, revision "+next.agendaVersion);
            }
            catch(Exception){meetingUI.SetStatus("Invalid shared agenda ignored.");}
            return true;
        }
        if(packet.type=="notice"){meetingUI.SetStatus(packet.message);return true;}
        return false;
    }
}

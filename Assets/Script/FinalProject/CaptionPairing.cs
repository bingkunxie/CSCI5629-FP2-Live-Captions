using System;
using System.IO;
using FP2.Captions;
using UnityEngine;

public sealed partial class QuestLiveCaptions
{
    private QuestPairTransport pair;
    private PairConfig pairConfig;
    private bool pairRequired, pairOnline, pairReady, pairHeld, pairStarting;
    private string pairTurn = "", pairOwner = "", pairRemotePhase = "", pairPhaseSent = "";
    private float pairLastState;
    private string pairLastResult = "", pairLoggedStatus = "";
    private bool PairChinese => direction == TranslationDirection.MandarinToEnglish;

    private void InitializePair()
    {
        string path = Application.isEditor ? Environment.GetEnvironmentVariable("FP2_PAIR_CONFIG") :
            Path.Combine(ConfigurationDirectory, "pair-session.json");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return; // Existing single-headset mode.
        pairRequired = true;
        try
        {
            pairConfig = JsonUtility.FromJson<PairConfig>(File.ReadAllText(path));
            if (pairConfig == null || string.IsNullOrWhiteSpace(pairConfig.host) || pairConfig.port < 1 ||
                pairConfig.port > 65535 || (pairConfig.role != "A" && pairConfig.role != "B") ||
                string.IsNullOrEmpty(pairConfig.token) || pairConfig.token.Length < 20) throw new IOException();
            pair = new QuestPairTransport(pairConfig);
            speaker = pairConfig.role;
        }
        catch (Exception) { Debug.LogWarning("[FP2] Pair configuration invalid; paired recording disabled."); }
    }

    private bool RequestPairTurn()
    {
        if (!pairRequired || pairStarting) return false;
        if (pairOnline && pairReady && pairOwner == pairConfig.role && !string.IsNullOrEmpty(pairTurn) &&
            session != null && session.State == CaptionState.Reviewing)
        {
            pairHeld = true; pairPhaseSent = "";
            pair.Send(new PairPacket { type="phase", turn=pairTurn, phase="speaking" });
            return false; // Re-record an unaccepted draft while retaining this device's lease.
        }
        if (!pairOnline || !pairReady || !string.IsNullOrEmpty(pairOwner) || !string.IsNullOrEmpty(pairTurn))
        { DrawPairStatus(); return true; }
        pairHeld = true;
        pairTurn = Guid.NewGuid().ToString("N");
        pairPhaseSent = "";
        pair.Send(new PairPacket { type="acquire", turn=pairTurn });
        statusText.text = PairChinese ? "正在申请发言，请稍等…" : "Requesting the speaking turn…";
        return true;
    }

    private void ReleasePairTurn()
    {
        pairHeld = false;
        if (!string.IsNullOrEmpty(pairTurn))
            pair?.Send(new PairPacket { type="release", turn=pairTurn });
        pairTurn = ""; pairPhaseSent = "";
    }

    private void StopPairTrigger()
    {
        pairHeld = false;
        // Trigger can be released before the server grant arrives.
        if (recording == null && !string.IsNullOrEmpty(pairTurn) &&
            (session == null || session.State != CaptionState.Transcribing &&
             session.State != CaptionState.Translating && session.State != CaptionState.Reviewing))
            ReleasePairTurn();
    }

    private void PairTick()
    {
        if (!pairRequired) return;
        if (pair != null)
        {
            while (pair.Received.TryDequeue(out PairPacket packet))
            {
                if (HandleMeetingPacket(packet)) continue;
                if (packet.type == "disconnected" || packet.type == "error")
                {
                    pairOnline = pairReady = false;
                    pendingAgendaSave = "";
                    CancelCurrentTurn();
                    pairOwner = "";
                }
                else if (packet.type == "state")
                {
                    pairOnline = true; pairReady = packet.paired;
                    pairLastState = Time.realtimeSinceStartup;
                    pairOwner = packet.owner ?? ""; pairRemotePhase = packet.phase ?? "";
                    // Lost lease: cancel local work; its late completion must not publish.
                    if (!string.IsNullOrEmpty(pairTurn) && !pairHeld &&
                        (packet.owner != pairConfig.role || packet.turn != pairTurn))
                        CancelCurrentTurn();
                    if (recording != null && (!packet.paired || packet.owner != pairConfig.role || packet.turn != pairTurn))
                        CancelCurrentTurn();
                    if (!string.IsNullOrEmpty(pairOwner) && pairOwner != pairConfig.role) voice?.Cancel();
                }
                else if (packet.type == "grant")
                {
                    if (packet.turn != pairTurn || !pairHeld || !isActiveAndEnabled)
                    {
                        if (packet.granted) pair.Send(new PairPacket { type="release", turn=packet.turn });
                        continue;
                    }
                    if (!packet.granted) { pairTurn = ""; pairHeld = false; continue; }
                    pairStarting = true;
                    try { BeginListening(); }
                    finally { pairStarting = false; }
                    if (recording == null) ReleasePairTurn();
                }
                else if (packet.type == "result" && packet.owner != pairConfig.role && packet.turn != pairLastResult)
                {
                    pairLastResult = packet.turn;
                    bool targetMatchesSource = TargetLanguageCode == packet.sourceLanguage;
                    string receivedCaption = targetMatchesSource ? packet.source : packet.translation;
                    if (!toolsOpen && !historyOpen)
                    {
                        captionText.text = receivedCaption;
                        captionText.pageToDisplay = 1;
                    }
                    // Received turns are displayed, never silently accepted into this headset's local notebook.
                }
            }
        }
        if (pairOnline && Time.realtimeSinceStartup - pairLastState > 4)
        {
            pairOnline = pairReady = false;
            CancelCurrentTurn(); pairOwner = "";
        }
        if (!string.IsNullOrEmpty(pairTurn) && session != null)
        {
            if (session.State == CaptionState.Error) ReleasePairTurn();
            else
            {
                string phase = session.State == CaptionState.Reviewing ? "reviewing" :
                    session.State == CaptionState.Transcribing || session.State == CaptionState.Translating ? "processing" : "";
                if (phase.Length > 0 && phase != pairPhaseSent)
                {
                    pair.Send(new PairPacket { type="phase", turn=pairTurn, phase=phase });
                    pairPhaseSent = phase;
                }
            }
        }
        DrawPairStatus();
    }

    private void PublishPairResult()
    {
        if (!pairRequired || string.IsNullOrEmpty(pairTurn)) return;
        pair.Send(new PairPacket { type="result", turn=pairTurn, source=session.Transcript,
            translation=session.Caption, sourceLanguage=PairChinese ? "zh" : "en" });
        pairTurn = ""; pairHeld = false; pairPhaseSent = "";
    }

    private void DrawPairStatus()
    {
        if (!pairRequired || statusText == null || toolsOpen || historyOpen) return;
        string label = null;
        if (!pairOnline) label = PairMessages.Offline(PairChinese);
        else if (!string.IsNullOrEmpty(pairOwner) && pairOwner != pairConfig.role)
            label = PairMessages.Waiting(pairOwner, pairRemotePhase, PairChinese);
        else if (!pairReady)
            label = PairChinese ? "等待另一台 Quest 连接…" : "Waiting for the other Quest to connect…";
        else if (session == null)
            label = PairChinese ? "双人会话已连接；请先配置课程密钥。" : "Pair connected; configure the course key before recording.";
        else if (session.State == CaptionState.Ready)
            label = PairMessages.Ready(PairChinese);
        if (label != null)
        {
            statusText.text = label;
            if (label != pairLoggedStatus)
            {
                pairLoggedStatus = label;
                Debug.Log("[FP2 Pair] " + label); // Fixed UI status only, never conversation text or tokens.
            }
        }
    }
}

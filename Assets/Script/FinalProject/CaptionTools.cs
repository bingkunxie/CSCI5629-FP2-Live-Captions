using System;
using System.IO;
using System.Text;
using FP2.Captions;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class QuestLiveCaptions
{
    private bool noiseReduction = true, reviewEveryTurn = true, notebookEnabled = true;
    private bool toolsOpen, historyOpen, clearArmed;
    private int toolsIndex, historyIndex, englishVoice, mandarinVoice;
    private string speaker = "A", notebookMessage = "", audioQuality = "";
    private ConversationNotebook notebook;
    private InputAction toolsAction;
    private int CurrentVoicePreset => TargetLanguageCode == "zh" ? mandarinVoice : englishVoice;

    private void InitializeTools()
    {
        try
        {
            notebook = new ConversationNotebook(Path.Combine(ConfigurationDirectory, "conversation-notebook.dat"));
            if (notebook.RecoveredFromBackup) notebookMessage = "Recovered backup";
        }
        catch (Exception) { notebookEnabled = false; notebookMessage = "Notebook unavailable; existing file retained"; }
        toolsAction = new InputAction("Caption tools", InputActionType.Button,
            "<XRController>{RightHand}/gripPressed", interactions: "Press(behavior=0)");
        toolsAction.AddBinding("<Keyboard>/f1");
        toolsAction.performed += _ => ToggleTools();
        toolsAction.Enable();
        // Status now includes localized Mandarin peer messages; reuse the dynamic CJK font.
        statusText.font = captionText.font;
        panel.sizeDelta = new Vector2(1000, 600);
        captionText.rectTransform.sizeDelta = new Vector2(944, 220);
        statusText.rectTransform.anchoredPosition = new Vector2(0, -294);
        statusText.rectTransform.sizeDelta = new Vector2(944, 60);
        modeText.rectTransform.anchoredPosition = new Vector2(0, -362);
        modeText.rectTransform.sizeDelta = new Vector2(944, 95);
        hintText.rectTransform.anchoredPosition = new Vector2(0, -470);
        hintText.rectTransform.sizeDelta = new Vector2(944, 110);
    }

    private void ApplyVoicePreset()
    {
        if (gateway != null) gateway.VoicePreset = CurrentVoicePreset;
    }

    public void ToggleTools()
    {
        bool open = !toolsOpen && !historyOpen;
        CancelCurrentTurn(); toolsOpen = open; historyOpen = false; clearArmed = false;
        if (open) DrawTools();
        else { captionText.text = session?.Caption ?? "Ready"; captionText.pageToDisplay = 1; RefreshView(); }
    }

    // Existing mapped buttons become menu navigation only while the tools view is visible.
    private bool HandleToolsInput(string command)
    {
        if (!toolsOpen && !historyOpen) return false;
        if (command == "close") { ToggleTools(); return true; }
        if (historyOpen)
        {
            int count = notebook?.Entries.Count ?? 0;
            if (command == "apply") ExportNotebook();
            else if (count > 0)
            {
                captionText.ForceMeshUpdate();
                if (command == "next" && captionText.pageToDisplay < captionText.textInfo.pageCount)
                { captionText.pageToDisplay++; return true; }
                historyIndex = (historyIndex + (command == "next" ? 1 : -1) + count) % count;
            }
            DrawHistory(); return true;
        }
        if (command == "apply") ActivateTool();
        else { toolsIndex = (toolsIndex + (command == "next" ? 1 : 8)) % 9; clearArmed = false; }
        if (toolsOpen) DrawTools();
        return true;
    }

    private void DrawTools()
    {
        string[] rows = {
            "Noise reduction  " + (noiseReduction ? "ON" : "OFF"),
            "Review every turn  " + (reviewEveryTurn ? "ON" : "OFF"),
            "Voice accent  " + VoicePresets.Name(TargetLanguageCode, CurrentVoicePreset),
            "Active speaker  " + speaker + (pairRequired ? " (paired device)" : " (manual label)"),
            "Save confirmed turns  " + (notebookEnabled ? "ON" : "OFF"),
            "Read notebook  (" + (notebook?.Entries.Count ?? 0) + " turns)",
            "Export notebook",
            clearArmed ? "Press B again to clear saved turns" : "Clear notebook",
            "Close tools"
        };
        // Show three options at a time to preserve readable text size in the headset.
        var text = new StringBuilder("TOOLS\n");
        int first = toolsIndex / 3 * 3;
        for (int i = first; i < Math.Min(first + 3, rows.Length); i++)
            text.Append(i == toolsIndex ? "> " : "  ").Append(rows[i]).Append('\n');
        captionText.text = text.ToString(); captionText.pageToDisplay = 1;
        statusText.text = "Tools " + (toolsIndex + 1) + "/9. One speaker per recording.";
        hintText.text = "X: previous option   |   A: next option   |   B: select\nY or RIGHT grip: close tools";
        RefreshModes();
    }

    private void ActivateTool()
    {
        switch (toolsIndex)
        {
            case 0: noiseReduction = !noiseReduction; break;
            case 1: reviewEveryTurn = !reviewEveryTurn; break;
            case 2:
                voice?.Cancel();
                if (TargetLanguageCode == "zh") mandarinVoice = (mandarinVoice + 1) % VoicePresets.Count("zh");
                else englishVoice = (englishVoice + 1) % VoicePresets.Count("en");
                ApplyVoicePreset(); break;
            case 3:
                if (!pairRequired) speaker = speaker == "A" ? "B" : "A";
                break;
            case 4:
                if (notebook != null) notebookEnabled = !notebookEnabled;
                break;
            case 5:
                toolsOpen = false; historyOpen = true;
                historyIndex = Math.Max(0, (notebook?.Entries.Count ?? 0) - 1);
                DrawHistory(); break;
            case 6: ExportNotebook(); break;
            case 7:
                if (!clearArmed) { clearArmed = true; return; }
                try { notebook?.Clear(); notebookMessage = "Notebook cleared"; }
                catch (Exception) { notebookMessage = "Clear incomplete; check notebook"; }
                clearArmed = false; break;
            case 8: ToggleTools(); break;
        }
    }

    private void SaveAcceptedTurn()
    {
        if (!notebookEnabled || notebook == null) return;
        try
        {
            notebook.Add(new ConversationEntry {
                Id = session.TurnId, UtcTime = DateTime.UtcNow.ToString("O"),
                Speaker = speaker, Direction = direction.ToString(),
                Source = session.Transcript, Translation = session.Caption,
                Reviewed = session.WasReviewed, ProcessingSeconds = session.ProcessingSeconds
            });
            notebookMessage = "";
        }
        catch (Exception) { notebookMessage = "Save failed; caption available"; }
    }

    private void DrawHistory()
    {
        if (notebook == null || notebook.Entries.Count == 0) captionText.text = "Your notebook is empty.";
        else
        {
            var e = notebook.Entries[historyIndex];
            captionText.text = "Speaker " + e.Speaker + "  " + e.UtcTime.Substring(0, 16) + "\n" + e.Source + "\n\n" + e.Translation;
        }
        captionText.pageToDisplay = 1;
        statusText.text = "Notebook " + (notebook == null || notebook.Entries.Count == 0 ? 0 : historyIndex + 1) +
            "/" + (notebook?.Entries.Count ?? 0) + "   |   " + notebookMessage;
        hintText.text = "X: previous turn   |   A: next page / turn   |   B: export\nY or RIGHT grip: return to captions";
    }

    private void ExportNotebook()
    {
        if (notebook == null) { notebookMessage = "Notebook unavailable"; return; }
        try
        {
            string directory = Path.Combine(ConfigurationDirectory, "NotebookExports");
            Directory.CreateDirectory(directory);
            string name = "Conversation-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".md";
            File.WriteAllText(Path.Combine(directory, name), notebook.ToMarkdown(), new UTF8Encoding(false));
            notebookMessage = "Export saved in app files";
        }
        catch (Exception) { notebookMessage = "Export failed; saved turns retained"; }
        RefreshModes();
    }
}

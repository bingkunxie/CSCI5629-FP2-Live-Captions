# Noise reduction, conversation notes and voice accents

This update extends the current Unity application. It retains bidirectional translation and existing gesture effects.

## In-headset walkthrough

1. Hold the right trigger while **one person** speaks, then release.
2. The app checks audio quality, conditions the audio, and shows the recognized source and the translation for review.
3. Use **A** to read subsequent pages. On the final page, **A** accepts the turn. Left trigger discards it. Recording another turn replaces the unaccepted draft.
4. An accepted turn enters the local notebook and, when voice is enabled, plays in the selected voice.
5. Press **right grip** (the side squeeze button) to open tools. **X** selects the previous option, **A** selects the next option, **B** applies it, and **Y** or right grip closes tools. Editor equivalents are F1, H, Right Arrow, V and L.
6. Choose Voice accent to cycle voices for the current translation output language. Exit tools and toggle voice with B to hear the last accepted translation. Changing accent stops pending speech.
7. Select Read notebook to browse accepted conversations. X goes to the previous turn. A advances through pages and then to the next turn. B exports all saved turns.

Tools also control noise reduction, review of every turn, manually selected Speaker A/B, notebook recording, Markdown export and notebook clearing. Clearing requires B twice on the Clear option. Explicitly exported Markdown files remain until removed separately.

## Noise handling

The default recording path downmixes the actual microphone channels, applies an 80 Hz high-pass filter and softly attenuates low-energy gaps. It never boosts the background. The filter can be disabled for comparison.

Very short recordings, near-silence and substantial clipping are rejected before sending audio to the gateway. Low envelope contrast or milder clipping requires transcript review even when Review every turn is disabled. Envelope contrast is a heuristic based on 20 ms energy windows, not a measured SNR or a speech-recognition confidence score. Steady speech can trigger this conservative review.

This reduces low-frequency rumble and quiet background gaps. Nearby competing speech can still reach the transcription service. The current mono microphone/transcript API does not reliably detect simultaneous speakers or separate their voices. Take turns, move closer to the intended speaker and repeat mixed turns. The app defaults to review before accepting so users can discard mixed content before it enters notes or speech output. Speaker A/B is a manual label, not automatic identification.

Starting a recording stops generated speech. Audio configuration changes cancel the active operation and allow another attempt. Physical USB/audio-route recovery still requires device verification.

## Conversation notebook

Accepted turns include the recognized original, translated text, UTC time, manual speaker label, language direction, review status and processing duration. A repeated translation is a new turn, while a repeated save of the same turn ID is deduplicated. Discarded or cancelled drafts are excluded.

Storage is private app data in files/conversation-notebook.dat. Atomic snapshots keep a backup for recovery. The notebook retains the latest 500 turns. Disable saving in Tools when desired. It stores text, not microphone recordings.

Exports are UTF-8 Markdown in private files/NotebookExports/. In a development build, copy explicit exports to your computer with:

    python Tools/export_notebook.py --adb "path/to/adb.exe" --out "D:/download/CODEX/notebook-exports"

Use a fresh output folder for a different headset or conflicting filenames. Notebook exports may contain conversation content and should remain outside the Git repository.

## Voice presets

The existing azure-tts course gateway receives a selected voice ID and the exact accepted translation.

| Output | Menu label | Voice ID |
|---|---|---|
| English | US English | alloy (existing gateway default) |
| English | UK English | en-GB-SoniaNeural |
| English | Australian English | en-AU-NatashaNeural |
| Mandarin | Mainland Mandarin | zh-CN-XiaoxiaoNeural |
| Mandarin | Taiwan Mandarin | zh-TW-HsiaoChenNeural |

The gateway's default alias preserves existing behavior. Regional voices require that voice to be enabled by the course gateway. An unavailable voice displays an error and leaves the caption usable. No silent fallback to another accent occurs. Voice is optional and starts off. Selecting a preset does not change the translation language.

Azure voice reference: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=text-to-speech

## Verification

Core regressions cover review/accept/discard, cancellation of old work, silence/clipping checks, low-frequency attenuation, channel handling, Unicode notebook round trips, deduplication, backup recovery, save failures and voice preset IDs. Existing caption, translated-voice and gesture regressions are also retained.

The full Unity EditMode regression suite now passes 54 tests, including these features and paired-headset logic. APK and hardware status is recorded separately. Campus-network testing is deferred. See [BUILD-STATUS.md](BUILD-STATUS.md) for the current verification status. Synthetic filter tests demonstrate signal processing behavior; recognition accuracy in noisy or overlapping conversation requires controlled device trials.

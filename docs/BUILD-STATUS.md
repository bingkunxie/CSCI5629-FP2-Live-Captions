# Final project build status

## Two-Quest implementation verification — November 11, 2026

- Implemented LAN turn arbitration, fixed device roles, source-language wait messages, peer result display and disconnect/timeout recovery. Added an optional USB pairing route for isolated Wi-Fi.
- Full Unity 6000.6.0f1 EditMode run: **54 passed, 0 failed**. This includes the prior caption/audio/notebook/voice tests, gateway request construction and new controller-level wait-language/capture-blocking tests.
- Python relay tests: **12 passed**, including two real TCP clients, competing requests, Unicode result delivery, role rejection, disconnect and lease expiry.
- Android build and hardware check are in progress. These results do not establish two-physical-headset behavior or real gateway accent availability.
- Course gateway tests remain deferred at the user's request.

## Earlier source checkpoint — November 11, 2026

This checkpoint adds audio conditioning and quality gates, review-before-publish, a private persistent conversation notebook with Markdown export, regional TTS voice selection and a right-grip tools menu. Existing bidirectional translation and gesture features are retained in source.

Verification for this checkpoint:
- 39 standalone core NUnit cases passed, 0 failed, covering existing caption/voice/gesture logic and the new audio, review and notebook logic.
- Regional voice HTTP request tests were added but have not yet run in Unity.
- Full Unity EditMode compilation/tests and Android APK build are pending. The initial Unity import failed in editor-shipped render-pipeline dependencies; missing files were traced to installer path length. Repair using a shorter D-drive installation alias is underway.
- The new checkpoint has not been installed or verified on Quest 3. Earlier device results below apply to previous builds.
- Course gateway voice checks timed out. Campus-network tests are deferred at the user's request. Regional voice availability and audible accents remain unverified.
- Audio filtering is conservative high-pass/gap attenuation, not speaker separation. Review and manual speaker labels help manage mixed speech; overlapping-speaker recognition accuracy is not established.
- Audio-route recovery logic was added, but physical USB hot-plug recovery remains unverified.

This is a source progress checkpoint, not a validated APK release. See [new controls and limitations](Conversation-Improvements.md).

## Earlier build history

Date: November 10, 2026.

The independent project passed 12 EditMode tests. The course transcription and translation endpoints passed a synthetic Mandarin test on eduroam: transcription 2.255 seconds and translation 1.505 seconds, with the time correction and library floor preserved. Those are computer-to-gateway results, not headset latency.

The previous APK produced a reproducible Android ANR. Android main thread waited in GameActivity.surfaceDestroyed; Unity main thread waited for first-scene asynchronous loads; the active loading thread was in ZipFile.InflateRead. No caption script startup marker appeared.

The new project removes the unused A4 inference package, including a roughly 100 MB compiled compute-shader resource seen in the previous APK. This is a targeted candidate fix, not yet a confirmed resolution. The new Android build and subsequent device test are in progress.

## Device verification after clean build

The initial incremental APK retained the obsolete shader despite the source package removal. A clean Android build removed it; `Tools/check_caption_apk.py` rejects the old artifact and passes the new artifact. Unity data fell from about 187.6 MB to 76.9 MB uncompressed.

The new app reached its Ready state and rendered the caption panel over Quest passthrough. A live microphone capture of 2.48 seconds produced the displayed English caption “Hello, may I ask what your name is?” using direct UMN HTTPS. Measured capture-end to caption frame: 3.04 seconds (transcription 1.32 s, translation 1.70 s). This is one observed utterance, not a benchmark average. A very quiet initial capture was rejected locally.

Evidence: `Logs/FP2/live-device.log`, `quest-caption-panel.png`, `quest-live-translation.png`, and `validation.json`. USB-disconnected confirmation and the speaker's meaning check are pending.

## Standalone confirmation and microphone limitation

The user confirmed that Mandarin-to-English translation works. Disconnecting USB while the app was already running caused repeated “too short” messages; reconnecting restored normal recordings. The user then fully closed the app, disconnected USB, and launched it from the headset. Translation worked in that untethered run on eduroam.

Standalone operation is therefore confirmed for a fresh launch without USB. Hot-plug microphone recovery remains unresolved. Demo procedure: disconnect USB before opening the app; restart the app after a USB/audio-route change if capture reports “too short.” Do not claim the hot-plug bug is fixed.

Additional capture metadata diagnostics were added (elapsed hold duration, microphone active state, sample position, clip frequency, and cancellation state). They do not log speech or credentials. The diagnostic source build retains `BuildOptions.Development`, because device credential provisioning depends on `adb run-as`; a release-build change would require separate fresh-install provisioning validation.

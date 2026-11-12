# Final project build status

## Source-language UI — November 12, 2026

- Added a shared 154-entry UI catalog. Mandarin source selects Simplified Chinese UI; English source selects English UI, independent of the translation target.
- Localized main labels, hints, capture/gateway errors, voice names, tools/notebook controls, meeting agenda editor and reaction notices. Existing notices/toasts refresh on source change; participant content and drafts remain unchanged.
- All relevant main-panel text uses the bundled CJK font. Repository AGENTS.md records the rule for subsequent development.
- Full Unity EditMode suite: **70 passed, 0 failed**. New integration tests cover both source directions, tools/notebook/hints, capture errors, gesture statuses, existing notices, draft preservation and popup lifetime.
- Android **0.3.1 / versionCode 4** built, passed decompressed credential/private-data and obsolete-shader checks, and installed on Quest 3. The app reached Ready on the existing working USB laptop relay.
- APK: 116,406,296 bytes; SHA-256: 490ccb1bc42b52058eca507d285e83c9a4b6bebf7f2ebace373933db224bbfd1.
- Quest system dialogs/keyboards follow OS settings; participant input and translated captions are intentionally not UI-translated.
- [Language behavior and development rule](UI-Language.md).

## Campus voice checkpoint — November 12, 2026

**AI speech playback passed the user-confirmed Quest test. Work paused after this feature at the user's request.**

- All five implemented voice presets returned valid, non-silent 24 kHz mono PCM WAV from the real gateway: alloy, en-GB-SoniaNeural, en-AU-NatashaNeural, zh-CN-XiaoxiaoNeural and zh-TW-HsiaoChenNeural.
- Headset direct access failed while it was connected to UofM-Guest. The laptop's gateway requests succeeded. Configured the existing USB loopback route and fixed the relay's macOS-only certificate-file path by using Python's default verified TLS context.
- Through the relay, actual headset transcription, translation and speech requests returned HTTP 200. The user confirmed audible English playback and normal behavior.
- One observed turn reached the caption in 3.02 seconds (transcription 1.20 s, translation 1.79 s); this is not a benchmark average.
- Voice availability was checked for all presets; individual subjective accent comparison inside the headset remains unverified.
- The running setup requires USB, the local relay and laptop campus/VPN access. The APK is unchanged at 0.3.0; the fix is in the laptop Python relay.
- No noisy-speech, overlapping-speaker, two-real-headset or camera-classifier acceptance was performed in this checkpoint.
- Evidence: [voice validation metadata](voice-validation-2026-11-12.json). No course credentials, user speech or notebook contents are included.

## Offline meeting tools — November 12, 2026

- Implemented four offline reactions, a collapsible translucent controller menu, native local thumbs-up shortcut, 2.5-second popup, and persistent shared six-point agenda with manual completion.
- Full Unity EditMode suite: **65 passed, 0 failed**, including actual overlay trigger consumption, draft conflict preservation, popup expiry and checkbox identity. Python: **20 passed** (7 meeting-feature tests and 13 relay tests), including real TCP clients and local HTTP editor.
- Android development APK **0.3.0 / versionCode 3** built successfully and installed on Quest 3; 116,397,352 bytes.
- SHA-256: 9a06f65a2c8dd675287c554c3878123b94f487962db733a381c0d8593589754f.
- Decompressed APK scan found neither saved course credentials nor pairing token/private app files. Obsolete A4 shader check passed.
- Actual Quest B connected over USB reverse to a simulated peer A without campus services. Runtime evidence confirmed all **four reaction displays** and **three agenda snapshots**, including an item completion update. The temporary probe restored prior private files byte-for-byte. The headset was then deliberately returned to standalone mode for immediate offline use with one available Quest; the prior pairing configuration remains backed up privately.
- The user confirmed the new agenda panel and Reactions button are visible in the headset. Physical controller selection, native thumbs-up usability and two-real-headset wireless acceptance remain separate from these automated/device-receive checks.
- Local browser rendering verified Chinese/English agenda text and checked-item strike-through.
- Campus speech/translation/TTS testing remains deferred. Sharing uses the local relay; standalone mode provides local agenda editing/checking and explicitly labeled reaction preview.
- [English controls](Meeting-Tools.md) · [中文操作说明](Meeting-Tools.zh-CN.md)

## Two-Quest implementation verification — November 11, 2026

- Implemented LAN turn arbitration, fixed device roles, source-language wait messages, peer result display and disconnect/timeout recovery. Added an optional USB pairing route for isolated Wi-Fi.
- Full Unity 6000.6.0f1 EditMode run: **56 passed, 0 failed**. This includes the prior caption/audio/notebook/voice tests, gateway request construction and new controller-level wait-language/capture-blocking tests and Android network-policy path/idempotency tests.
- Python relay tests: **12 passed**, including two real TCP clients, competing requests, Unicode result delivery, role rejection, disconnect and lease expiry.
- Android development APK built successfully: 116,377,560 bytes. The obsolete A4 shader check passed. A scan of decompressed APK entries found neither the configured course key nor the private pairing token, and no private notebook/config files.
- APK SHA-256: 5532d7feb3f37949f361336bb7bf3347457701ac77e3d0ef9612d207382fe13e.
- Installed the APK on the connected Quest 3 and provisioned private configuration. Quest-to-laptop pairing protocol connectivity passed through USB reverse (real device received state messages). Direct Wi-Fi TCP connectivity did not pass on this network; creating a narrowly scoped firewall rule was denied by Windows, so no firewall change was made.
- The initial launch was blocked by a Quest system dialog. After the user woke the headset and the previous application was closed, FP2 launched successfully. App logs confirmed localized speaking/processing and ready messages received through the paired session. The user confirmed the caption panel is clearly visible and displays normally.
- A simulated peer exercised the protocol; no synthetic conversation was added to the notebook. A second physical headset is unavailable, and the user explicitly deferred that trial. Do not treat this as two-physical-headset verification. The installed headset was returned to single-headset mode to avoid waiting for an absent peer.
- Two-physical-headset behavior and real gateway accent availability remain unverified.
- Windows build fixes: independent Android toolchain, short Gradle cache alias to avoid the 260-character Ninja limit, and a callback that accepts either Gradle-root or unityLibrary paths while preserving HTTPS-only defaults and loopback-only development HTTP.
- The Meta SDK emitted an editor telemetry Process.Kill exception after Unity reported build success. The produced APK passed structural checks and installed successfully; this exception was not an Android application crash.
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

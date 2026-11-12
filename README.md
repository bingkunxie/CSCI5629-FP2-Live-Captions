Independent Unity project for Quest 3 Mandarin–English captions, optional translated speech, and thumbs-up effects.

- Unity: 6000.6.0f1, Universal Render Pipeline, Meta XR / OpenXR.
- Main scene: `Assets/Scenes/FP2LiveCaptions.unity`.
- Runtime source: `Assets/Script/FinalProject`.
- Tests: `Assets/Tests/FP2/Editor`.
- Android application: `edu.umn.csci5629.livecaptions`.

Reuses the course gateway HTTP/WAV helpers and Quest setup from A3. Assignment scenes and A2/A3/A4 application scripts have been removed. Caption cancellation, paging, controller mappings and UI are retained.

Hold the right trigger to record; release to translate. Left trigger cancels, X changes height, and A advances long captions. B toggles translated voice, Y switches translation direction, and left grip toggles camera gesture detection. See [feature controls and validation](docs/Features.md).

Use the existing course key via `UMN_GATEWAY_KEY_FILE` in the Editor. No credential was copied into this project. Android setup writes the existing key into private app data using `Tools/fp2_device_setup.py`.

The prior APK had an Android startup ANR. Its loader was blocked in ZIP decompression during first-scene loading, and the APK contained a roughly 100 MB compiled compute shader from the unused A4 inference package. A clean build now excludes that package and its cached data. The app has started successfully on Quest and displayed a live English translation using direct UMN HTTPS. The observed capture-end-to-caption-frame delay was 3.04 seconds for one utterance.

Previous baseline validation (before the conversation improvements below): the independent project imported and compiled; all 35 expanded-feature EditMode/integration tests passed (`Logs/FP2/features-reviewed-tests.xml`). The assignment projects remain unchanged. Device startup and one live speech-to-caption flow have been observed; see `docs/BUILD-STATUS.md` for the exact evidence and remaining checks.

Standalone test: the user confirmed successful translation after disconnecting USB and launching the app fresh on eduroam. For the demo, unplug before launching. Changing USB while the app is running may require an app restart to recover microphone capture.

## Conversation improvements

**Progress, November 11, 2026:** the full Unity EditMode run passed 54 tests, and 12 Python relay tests passed. See [build and device verification](docs/BUILD-STATUS.md) for APK and hardware status.

Noise conditioning, transcript review, a persistent conversation notebook and selectable speech accents are implemented from the **right grip** tools menu. Review is on by default. Press A through the draft pages and again on the last page to accept, save and optionally speak the turn. See [controls and limits](docs/Conversation-Improvements.md).

## Two-headset turn-taking

Optional LAN pairing gives each Quest a fixed A/B identity. While A records, B displays a waiting message in **B's selected source language** and cannot start a competing recording. A local Python relay arbitrates simultaneous requests, tracks disconnects and forwards confirmed results. This coordination works without campus gateway access; speech processing still needs the gateway. See [pair setup, controls and offline checks](docs/Two-Quest-Session.md).

## Open and run

1. Clone this repository and open its root folder in Unity Hub with Unity 6000.6.0f1 and Android Build Support installed.
2. Let Unity resolve the packages, then open `Assets/Scenes/FP2LiveCaptions.unity`.
3. Obtain your own authorized UMN course gateway access. Keep the key outside the repository and set `UMN_GATEWAY_KEY_FILE` to its private file path for Editor testing.
4. Follow the [run guide](docs/Run-Guide.md) for building, device setup, and controls.

The gateway requires campus network access or the appropriate UMN VPN route. Credentials, APKs, recordings, logs, and Unity-generated caches are intentionally excluded from this repository.

## Credits and dependencies

This course prototype reuses the instructor-provided A3 HTTP/WAV helpers and uses Unity, Meta XR, TextMeshPro, and their dependencies. Existing third-party license and notice files are retained; their terms continue to apply. No additional project-wide license is asserted here.

Expanded features: the user confirmed translated English speech, readable Chinese with Mandarin speech, voice-off behavior, the wearer thumbs-up popup, and the other-speaker camera popup. See [Features](docs/Features.md) for controls and limitations.

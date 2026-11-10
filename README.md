# CSCI 5629 Final Project

Independent Unity project for Quest 3 Mandarin-to-English captions. This is not a Git worktree of the assignment repository.

- Unity: 6000.6.0f1, Universal Render Pipeline, Meta XR / OpenXR.
- Main scene: `Assets/Scenes/FP2LiveCaptions.unity`.
- Runtime source: `Assets/Script/FinalProject`.
- Tests: `Assets/Tests/FP2/Editor`.
- Android application: `edu.umn.csci5629.livecaptions`.

Reuses the course gateway HTTP/WAV helpers and Quest setup from A3. Assignment scenes and A2/A3/A4 application scripts have been removed. Caption cancellation, paging, controller mappings and UI are retained.

Hold the right trigger to record Mandarin; release to translate. Left trigger cancels, X changes height, A advances long captions. Voice output is not implemented in this milestone.

Use the existing course key via `UMN_GATEWAY_KEY_FILE` in the Editor. No credential was copied into this project. Android setup writes the existing key into private app data using `Tools/fp2_device_setup.py`.

The prior APK had an Android startup ANR. Its loader was repeatedly decompressing a 100 MB compute shader from the unused A4 inference package. This project excludes that package; on-device startup and live caption validation must be repeated before claiming the issue fixed.

Validation: independent project imports and compiles; all 12 FP2 EditMode tests passed (`Logs/tests.xml`). The assignment projects remain unchanged. Device startup validation is still outstanding.

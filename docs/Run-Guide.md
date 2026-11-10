# FP2 Mandarin-to-English captions

This first implementation records a short Mandarin utterance on Quest 3 and requests an English translation through the UMN course gateway. It is turn-based, not continuous streaming. It does not synthesize translated voice yet.

## Project and scene

- Isolated project: `/Users/bingkunxie/Unity-Projects/CSCI5629-FinalProject`
- Branch: `main`
- Scene: `Assets/Scenes/FP2LiveCaptions.unity`
- Android package: `edu.umn.csci5629.livecaptions`
- APK: `build/FP2/FP2-Live-Captions.apk`
- Unity: 6000.6.0f1

This is an independent Unity project and repository. It reuses the caption system, course gateway helpers, and Quest configuration. Assignment scenes/scripts and the unused local inference package are excluded. Credentials and generated caches are not committed.

## Controls

| Action | Quest controller | Editor keyboard |
|---|---|---|
| Record the other speaker | Hold right index trigger | Hold Space |
| Finish capture and translate | Release right trigger | Release Space |
| Cancel capture/request | Left trigger | Escape |
| Change panel height | X | H |
| Next page of a long translation | A | Right arrow |

Recordings stop automatically after 15 seconds. Too-short or very quiet input is rejected locally. A previous translation remains visible during a new request and is labeled as previous. Long translations use pages at a stable font size. Networking and processing states remain visible.

## Course services

- Speech transcription: `gemini-3.5-transcribe-preview`, explicitly `language=zh`.
- Translation: `gpt-6-luna`, with a translation-only instruction and no chatbot conversation history.
- Endpoint: `https://api.aigateway.umn.edu`.
- Course model access and campus/VPN connectivity are required.

The app stores the existing course credential in Android's private internal `getFilesDir()` directory, not shared/external storage. No key is embedded in the scene, source, or APK. Keep the local key file, device data, and credentials out of submissions and screenshots.

## Build and install

From the isolated project directory, build with:

```sh
'/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -projectPath "$PWD" \
  -executeMethod FP2Setup.BuildBatch -logFile "$PWD/Logs/FP2/android-build.log"
python3 Tools/package_fp2_usb.py
```

The packaging step retains verified HTTPS and permits HTTP only for localhost, for the explicitly selected USB development route.

Install using the existing course key file:

```sh
python3 Tools/fp2_device_setup.py \
  --key-file '/Users/bingkunxie/Unity-Projects/csci5629-A1/.secrets/umn-key.txt' \
  --route direct
```

The script installs this separate application, writes the key through stdin into its app data, and launches it. Allow microphone access in the headset when prompted. For Editor testing, set `UMN_GATEWAY_KEY_FILE` to the existing key-file path before starting the isolated Editor, or use the same local `.secrets/umn-key.txt` convention.

## USB/VPN fallback

If the Quest cannot reach UMN directly, connect UMN VPN on the computer and run:

```sh
UMN_GATEWAY_KEY_FILE='/Users/bingkunxie/Unity-Projects/csci5629-A1/.secrets/umn-key.txt' \
  python3 Tools/a3_usb_relay.py
```

In another terminal, use `fp2_device_setup.py` with `--route usb`. Keep USB and the relay running. The headset displays `USB / laptop VPN relay`; do not describe that run as standalone. Re-run setup with `--route direct` to remove the route override. Only development builds honor the loopback route file.

## First headset check

1. Wear the Quest and allow microphone access. Confirm the panel appears over passthrough.
2. Have a nearby teammate speak while the wearer holds the right trigger. Begin at a comfortable conversational distance and check that the other person's voice is captured clearly.
3. Try: `明天的会议不是三点，是四点。我们在图书馆二楼见。`
4. Release the trigger. Confirm the English caption preserves **four, not three**, and **second floor of the library**.
5. Try a new question not present in the scripted WebXR phrases. The output should translate the question, not answer it.
6. Repeat a phrase, cancel an in-progress turn, and try a short/silent press. Confirm the next valid utterance can still complete.

Use X to compare panel positions with the same caption. A cycles pages if a translation is longer than the panel. Face visibility and actual headset readability still require a wearer check.

## Validation and measurements

- Automated results: `Logs/FP2/final-tests.xml`.
- Build output: `Logs/FP2/android-final-build.log` and `Logs/FP2/package-final.log`.
- Runtime log marker: `[FP2]`.
- `capture_to_caption_frame_s` measures trigger-release/capture-end to the first completed rendering frame after assigning the new caption. It is not a direct photon measurement or an independently measured speech-end timestamp.
- `stt_s` and `translation_s` measure the two service stages. These exclude recording time.
- Logs contain state/timing and input RMS, not audio, transcript contents, or credentials.

For a service-only test, run `Tools/fp2_validate_gateway.py` with `--key-file`, `--wav`, and `--output`. Use nonsensitive synthetic fixtures. This exercises services from the computer, not the headset microphone or trigger interaction. Keep that evidence separate from a live device test.

Current implementation validation is tracked in `docs/BUILD-STATUS.md`; do not infer live device success from a passing build or unit tests.

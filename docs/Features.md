# Voice, language switching, and gesture effects

## Controls

- Right trigger: hold to record, release to translate.
- Left trigger: cancel recording, translation, or speech.
- **B:** translated voice on/off (off at launch).
- **Y:** Mandarin → English / English → Mandarin. Switching cancels the current turn and clears its text.
- X: lower/upper caption panel.
- A: next page of a long caption.
- **Left grip:** camera gesture mode on/off (off at launch). This is the side squeeze button under the left middle finger, not the front index-finger trigger.
- Editor equivalents: Space, Escape, V, L, H, Right Arrow, G.

Captions appear before voice is generated. English uses the course service's `alloy` voice; Mandarin uses `zh-CN-XiaoxiaoNeural`. Muting or starting a new recording stops playback and invalidates pending speech. Chinese text uses unmodified Noto Sans CJK SC Regular with its SIL OFL license.

## Thumbs-up effects

For the wearer's hand, put controllers down, wait for hand tracking, and hold a clear thumbs-up with the other four fingers curled. The local pose must remain stable for 0.25 seconds. Release to rearm.

For the other speaker, enable camera mode with the left grip and grant camera permission. Hold a real thumbs-up in front of the headset for a few seconds. Camera mode samples at most once every three seconds, sends a transient JPEG to the course vision model, and allows only one request in flight. It detects any clearly visible thumbs-up, without identifying whose hand it is.

Both paths share a four-second cooldown. Camera results older than five seconds are discarded, and snapshots of already-consumed wearer poses cannot produce delayed duplicates. Wait briefly between demonstrations and lower the hand between gestures. Camera mode turns off when the app pauses. It does not save frames.

The gold popup sits beside the caption panel. Oversized virtual hand meshes inherited from the rig are hidden while tracking/data providers remain enabled; passthrough still shows the real hands.

## Validation status

35 automated tests passed, including the hand-visual reference checks. Android compilation and packaging also passed. Course API checks returned valid English and Mandarin WAV audio and a valid negative classification for a blank image. The main gesture logic and delayed-result regressions passed.

The user confirmed English translated speech, readable Chinese with spoken Mandarin, voice-off caption-only behavior, and the wearer thumbs-up popup on Quest. The user also confirmed the gold popup from the other-speaker camera test. These confirmations are functional smoke tests, not accuracy measurements.

Known existing limitation: changing USB while the app runs can disrupt microphone capture. Disconnect before launch, or restart after changing USB.

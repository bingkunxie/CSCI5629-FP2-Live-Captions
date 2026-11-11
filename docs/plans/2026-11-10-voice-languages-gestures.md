# Voice, language switching, and thumbs-up effects

Requested: optional translated speech, Mandarin/English direction switching, and thumbs-up effects for both the wearer and the other speaker.

## Controls and behavior

- B toggles translated voice, off by default. Captions appear before voice generation. Muting, starting a new recording, changing direction, canceling, or suspending the app stops playback and invalidates pending speech.
- Y switches Mandarin-to-English / English-to-Mandarin. The selected direction is visible. Switching cancels the old turn and clears its caption. Chinese captions use a licensed CJK font.
- Existing right trigger recording, left trigger cancellation, X height, and A page controls remain.
- Wearer thumbs-up uses existing Meta hand tracking when hands are tracked. A brief stable pose produces one pop; release is required to rearm.
- Other-speaker thumbs-up uses a separately enabled camera mode (left grip). It samples headset camera frames for a binary thumbs-up judgment through the course gateway, with one request in flight and a bounded sampling interval. It can detect any visible thumbs-up, without claiming reliable attribution of whose hand it is.
- Camera mode defaults off. Camera permission is requested when enabled. Frames are not saved. No local inference package is added.
- Both recognition paths share a cooldown to avoid repeated effects. The pop is placed beside the caption panel, away from its text.

## Implementation order

1. Direction-aware gateway/session contracts and tests; preserve default Mandarin-to-English behavior.
2. CJK font and scene language/voice controls, with mode indicators.
3. Cancellable translated-voice generation/playback using the approved course speech service; tests for mute and stale results.
4. Native hand-pose recognition, camera-frame adapter/classifier, and a shared debounced effect trigger.
5. EditMode/integration tests, clean APK inspection, headset checks of both directions and voice toggling, then separate wearer/other-speaker gesture checks.

## Acceptance

- English speech yields a readable Mandarin caption after switching.
- Synthesized voice reads the exact translated caption, in the target language.
- Voice is silent when disabled; stale speech never plays after a new turn/direction.
- Local and camera gestures trigger a visible effect without continuous spam.
- Camera failure/denial does not block captions or local hand tracking.
- Preserve a working development build and `adb run-as` credential provisioning.

The user explicitly revoked the earlier 5 p.m. cutoff and instructed implementation to continue. Completion still requires implementation and headset testing.

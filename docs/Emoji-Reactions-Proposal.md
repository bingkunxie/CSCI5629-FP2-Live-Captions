Implementation update, November 12, 2026: the offline menu, four reactions, native thumbs-up shortcut and 2.5-second paired popup are now implemented. See [current controls](Meeting-Tools.md). The text below records the earlier design discussion; its future-tense statements are historical.

# Proposed shared emoji reactions

Status: design proposal, not implemented in this build.

## Interaction

A small translucent Reactions button sits beside the caption panel. Opening it shows four large targets: thumbs-up, applause, heart and question. A controller ray plus trigger selects a target, sends one message and collapses the picker. Clicking the picker must consume the trigger so it does not also start microphone capture.

The receiving headset shows the sender role plus a reaction graphic for 2.5 seconds, with a short fade. Position the toast above the caption panel, outside subtitle/status areas. Reactions remain usable while the other participant owns the speaking turn and do not change the microphone lock.

Use bundled sprites rather than relying on platform color-emoji fonts. Localize menu labels using the viewer's source language. Display-only transient reactions should not enter the conversation notebook by default.

## Gesture shortcut

Reuse the existing local Meta hand-tracking pose path for thumbs-up -> like. Local hand tracking does not need the campus gateway. Keep the optional camera/model classifier out of automatic emoji sending so seeing another person's hand does not falsely send a reaction as the wearer.

The existing ThumbsUpGate already requires a stable pose, release before retriggering and a four-second cooldown. Add a distinct accepted-native-gesture event rather than forwarding the current shared visual Pop, because Pop is also called by remote camera inference. Expose a shortcut on/off setting. Additional gestures should be added only after checking conflicts with pinch, menu selection and normal conversation gestures.

## Pair protocol

Add a reaction packet with an allow-listed ID, unique event ID and server-assigned sender role. The relay derives identity from the connection, rejects unknown IDs and rate-limits each sender independently. Reactions must not acquire/release the speaking turn or extend its lease.

On receipt, deduplicate IDs and start a 2.5-second local animation. Keep at most one visible toast and a small bounded queue; discard old queued reactions. Do not replay reactions after reconnecting.

## Verification before release

- Selection sends one packet and does not start recording.
- A reaction reaches the partner while the speaker lock is held.
- A held thumbs-up sends only once, tracking loss does not re-arm it, and release/retry respects cooldown.
- Duplicate, invalid and excessive messages are rejected.
- The toast disappears within 2–3 seconds and does not cover captions.
- Check controller rays, local hand tracking and readability on real headsets.

Suggested first increment: controller picker, four reaction sprites, paired 2.5-second toast, then native thumbs-up shortcut. The current two-Quest implementation supplies the transport foundation but does not yet include these reactions.

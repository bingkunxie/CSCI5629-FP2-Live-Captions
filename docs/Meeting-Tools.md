# Offline reactions and meeting agenda

Both features run locally and do not call the UMN gateway. Speech recognition, translation and synthesized speech still require the gateway.

## Headset controls

- Press the right thumbstick (F2 in Editor) or point at **Reactions +** and press the right trigger to expand the translucent reaction menu.
- Point the right controller at Like, Applause, Love or Question and press the right trigger. The receiver sees the sender and graphic for 2.5 seconds. The sender also sees confirmation.
- Native tracked-hand thumbs-up sends Like. Put controllers down and enable Quest hand tracking. Hold a clear thumbs-up briefly, release before repeating. The menu can disable this shortcut. Camera/cloud gesture classification never sends remote reactions.
- Reactions are rate-limited, deduplicated and not stored or replayed on reconnect. Single-headset mode explicitly shows **Local preview**. Paired mode requires the partner to be connected.
- The agenda stays beside the caption panel throughout the meeting. Point at a row and press the right trigger to check or uncheck it.
- Press the left thumbstick (F3 in Editor) or **Edit agenda** to write up to six points, at most 60 UTF-16 units per point (ordinary Chinese/English characters count as one).
- The built-in VR keyboard works offline. Add/delete/select points, then Save. Keyboard / 中文 requests the system keyboard where supported; Chinese input also works through the laptop editor below.
- Operating the palette/editor consumes the recording trigger and cancels an active local recording. Existing right-grip notebook/tools controls remain available.

The private agenda survives app restarts. In paired mode the relay is authoritative: both participants see the same points and completion states. Unchanged points keep their checks when reordered; changed text starts unchecked. Concurrent text edits detect revision conflicts instead of silently overwriting. Different item checks merge. A disconnected paired headset can view its cached agenda but cannot claim an edit was synchronized.

## Offline laptop editor

Run the normal pairing relay using a private configuration directory outside the repository:

    python Tools/quest_pair_relay.py --host LAPTOP_LAN_IP --config-dir PRIVATE_ROOM_DIRECTORY

Open http://127.0.0.1:8767 on that laptop. Enter one point per line, save, or toggle the checkboxes. Connected headsets receive changes immediately. Unicode/Chinese input uses the laptop's normal input method. Agenda data is saved as meeting-agenda.json in the private room directory.

Both headsets need local network access to the relay, but no internet or campus connection. Use the existing [pairing instructions](Two-Quest-Session.md) for role provisioning. The browser editor binds to laptop loopback only. USB reverse is available for one-headset development validation.

## Standalone agenda import

The laptop editor can export the saved agenda as meeting-agenda-import.json. Alternatively prepare a UTF-8 text file with one point per line. With USB debugging authorized:

    python Tools/configure_meeting_agenda.py --adb PATH_TO_ADB --serial QUEST_SERIAL --file PATH_TO_TEXT_OR_EXPORTED_JSON

Restart FP2 in standalone mode. The import is applied to private app storage. In paired mode use the shared relay/editor instead; an import does not override a shared room.

## Scope

This release supports one A/B room with two participants. Hand shortcuts currently bind thumbs-up to Like; other reactions use controller selection. No automatic meeting summary, speech-driven completion, cloud sync or arbitrary gesture rebinding is claimed. Actual two-headset wireless and physical hand-pose usability remain device acceptance checks; offline automated validation does not establish those hardware results.

[中文操作说明](Meeting-Tools.zh-CN.md)

Controller binding correction in 0.3.2: [thumbstick input details and offline tests](Controller-Input.md).

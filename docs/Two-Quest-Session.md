# Two Quest headsets: turn-taking and translated wait messages

Both headsets run the same development APK. A laptop on the same trusted Wi-Fi runs the Python coordinator. This pairing channel does not call the campus gateway. Speech recognition, translation and TTS still use the existing gateway.

## Behavior

- Each device has a fixed identity, A or B. The server grants only one speaking turn at a time. The recording starts after the grant, so wait for the Listening status before speaking.
- While A holds the right trigger, B sees "A is speaking. Please wait." when B's **source** language is English, or "A 正在说话，请稍等。" when B's source language is Mandarin. This follows B's direction setting, not A's language and not B's target language.
- Releasing the trigger changes the peer status to processing; the review stage shows a separate reviewing message.
- Accepting the draft sends both original and translation to the partner. The partner displays the text matching its target language. Received text is display-only: it is not automatically added to the receiving notebook or spoken. The sender retains the existing confirmed-note/TTS behavior.
- Cancellation, rejected audio, timeout and disconnect release the turn. Old replies/cancellations cannot cancel a newer turn.
- Releasing the trigger before a grant arrives does not start a recording.
- With pairing configured, both peers must be connected before recording. A broken relay does not silently fall back to independent recording.
- Each client sends a heartbeat every second. The server expires disconnected clients after six seconds; a speaking/processing/review turn has a hard 90-second limit. The client cancels active work after losing state updates for four seconds. Reconnect automatically, then press the trigger again.
- These controls coordinate application recordings; they do not separate physically overlapping voices or prevent someone from speaking out loud.

## Start on the laptop

Use Python 3.10 or newer (standard library only). Find the laptop's Wi-Fi IPv4 address with ipconfig. Use a Wi-Fi network that permits device-to-device traffic; some campus/guest networks isolate clients.

    python Tools/quest_pair_relay.py --host 192.168.1.50 --config-dir D:/download/CODEX/quest-pair-private

Replace the sample IP with the actual laptop address. The service listens on TCP port 8766. If Windows asks, allow Python on your trusted private network. Keep the terminal running. The config directory receives pair-A.json, pair-B.json and a random room token; retain it to reuse the same room after restarting.

Traffic is plaintext on the trusted LAN, including accepted captions and the room token. Do not expose/forward port 8766 to the Internet. No course key, audio or gateway credentials travel through this coordinator. The server does not persist conversations.

## Configure each Quest once

Install the new development APK first. With USB debugging authorized, obtain serial numbers using adb devices, then run:

    python Tools/configure_quest_pair.py --adb "path/to/adb.exe" --serial QUEST_A_SERIAL --config D:/download/CODEX/quest-pair-private/pair-A.json
    python Tools/configure_quest_pair.py --adb "path/to/adb.exe" --serial QUEST_B_SERIAL --config D:/download/CODEX/quest-pair-private/pair-B.json

Restart the app on each device. Configure their course keys separately using the existing device setup tool. The room config is stored in private app files, outside the APK. Assign different roles; a duplicate role is refused. The notebook's speaker selection is locked to that device's role in paired mode.

To return to single-headset mode:

    python Tools/configure_quest_pair.py --adb "path/to/adb.exe" --serial QUEST_SERIAL --disable

Restart the app. In the Unity Editor, set FP2_PAIR_CONFIG to an absolute path to one of the generated config files before launching the Editor. Without a pairing config, existing single-headset behavior remains unchanged.

## Offline checks

Run the server tests without any gateway access:

    python -m unittest discover -s Tools -p test_quest_pair_relay.py -v

These test two real TCP clients contending for a turn, result transfer, invalid/duplicate identities, cancellation isolation, peer disconnection and lease expiration. Unity tests verify that the actual caption controller selects the receiving device's source language and blocks its capture path while the peer owns the turn.

A two-physical-headset trial is still required for controller timing, Wi-Fi latency and real-world readability. See BUILD-STATUS.md for verified build and device results.

For a single-headset offline UI check, configure the real headset as B, start the relay, and simulate A:

    python Tools/simulate_quest_peer.py --config D:/download/CODEX/quest-pair-private/pair-A.json --seconds 8

This drives speaking, processing and reviewing messages, then releases the turn. It sends no audio or synthetic conversation text and writes no notebook entry. It tests status handling only, not live speech or two physical controllers.


If the Wi-Fi blocks client traffic or the laptop firewall cannot be configured, add --usb to configure_quest_pair.py. It uses adb reverse for the pairing port and stores 127.0.0.1 on that headset. Keep USB connected; repeat provisioning after reconnecting/rebooting to restore the reverse mapping. Both headsets can be tested through the same laptop with their respective serials and roles. Remove --usb and provision the LAN config again for wireless use.

## Windows build note

For Unity 6000.6 on Windows, keep the editor, project and GRADLE_USER_HOME paths short. Ninja can reject long paths inside Gradle's transformed GameActivity dependency even when the main project uses a short alias. A junction under D:/download/CODEX can preserve task storage while providing a short cache path. Use an independent Android toolchain for this editor; do not run installers through links to another editor's toolchain.

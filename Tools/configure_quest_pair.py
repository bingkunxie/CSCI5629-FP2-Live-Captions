"""Provision one Quest's private pairing config; no API key is needed."""
import argparse
import json
import subprocess
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--serial", required=True)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--config", type=Path)
    group.add_argument("--disable", action="store_true", help="Remove pairing config and restore single-headset mode after restart")
    parser.add_argument("--usb", action="store_true", help="Use adb reverse for offline testing when Wi-Fi isolates clients")
    args = parser.parse_args()
    if args.disable:
        result = subprocess.run([args.adb, "-s", args.serial, "shell", "run-as",
                                 "edu.umn.csci5629.livecaptions", "rm", "-f", "files/pair-session.json"],
                                capture_output=True)
        if result.returncode:
            raise SystemExit("Could not remove pairing config.")
        print("Pairing disabled. Restart the app for single-headset mode.")
        return
    data = args.config.read_bytes()
    config = json.loads(data)
    if config.get("role") not in ("A", "B") or len(config.get("token", "")) < 20:
        raise SystemExit("Invalid pairing config")
    if args.usb:
        result = subprocess.run([args.adb, "-s", args.serial, "reverse", "tcp:" + str(config["port"]),
                                 "tcp:" + str(config["port"])], capture_output=True)
        if result.returncode:
            raise SystemExit("USB reverse setup failed.")
        config["host"] = "127.0.0.1"
        data = json.dumps(config).encode("utf-8")
    command = "run-as edu.umn.csci5629.livecaptions sh -c 'umask 077; mkdir -p files; cat > files/pair-session.json'"
    result = subprocess.run([args.adb, "-s", args.serial, "shell", command], input=data, capture_output=True)
    if result.returncode:
        raise SystemExit("Provisioning failed. Install the development APK and authorize USB debugging.")
    print("Pair config installed for role " + config["role"] + ". Restart the app to connect.")
if __name__ == "__main__":
    main()

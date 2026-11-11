"""Copy explicitly exported Markdown notebooks from this development app over USB."""
import argparse
from pathlib import Path
import re
import subprocess

PACKAGE = "edu.umn.csci5629.livecaptions"

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True, type=Path, help="Destination directory on your computer")
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--serial")
    args = parser.parse_args()
    prefix = [args.adb] + (["-s", args.serial] if args.serial else ["-d"])
    def run(*command):
        result = subprocess.run(prefix + list(command), capture_output=True)
        if result.returncode:
            raise SystemExit("Could not access notebook exports. Connect the authorized headset and export from the Tools menu first.")
        return result.stdout
    names = run("exec-out", "run-as", PACKAGE, "ls", "files/NotebookExports").decode("utf-8").splitlines()
    args.out.mkdir(parents=True, exist_ok=True)
    count = 0
    for name in names:
        if not re.fullmatch(r"Conversation-\d{8}-\d{6}-\d{3}\.md", name):
            continue
        content = run("exec-out", "run-as", PACKAGE, "cat", "files/NotebookExports/" + name)
        destination = args.out / name
        if destination.exists():
            if destination.read_bytes() != content:
                raise SystemExit("An existing export has different content. Choose a new output folder.")
            continue
        destination.write_bytes(content)
        count += 1
    print(f"Copied {count} notebook export(s) to {args.out.resolve()}")

if __name__ == "__main__":
    main()

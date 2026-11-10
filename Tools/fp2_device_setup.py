"""Install the isolated FP2 app and configure its existing course credential.

Secrets travel over stdin into the app's own data directory, never command
arguments or logs. Does not alter the A3 application's data.
"""
import argparse
from pathlib import Path
import shlex
import subprocess

PACKAGE = 'edu.umn.csci5629.livecaptions'
DATA = 'files'  # Relative to run-as's private /data/user/<user>/<package> directory.
ROOT = Path(__file__).resolve().parents[1]


def adb(*args, data=None):
    result = subprocess.run(['adb', *args], input=data, capture_output=True, check=False)
    if result.returncode:
        # Do not echo arguments/input or device output for credential operations.
        raise RuntimeError('ADB operation failed; check the authorized Quest connection.')
    return result.stdout.decode(errors='replace').strip()


def write_private(name, data):
    command = 'umask 077; mkdir -p ' + shlex.quote(DATA) + '; cat > ' + shlex.quote(DATA + '/' + name)
    adb('shell', 'run-as ' + PACKAGE + ' sh -c ' + shlex.quote(command), data=data)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--key-file', required=True, type=Path)
    parser.add_argument('--route', choices=['direct', 'usb'], default='direct')
    parser.add_argument('--apk', type=Path, default=ROOT / 'build/FP2/FP2-Live-Captions.apk')
    args = parser.parse_args()
    key = args.key_file.read_bytes().strip()
    if not key or not args.apk.is_file():
        raise SystemExit('A built APK and an existing course key file are required.')
    if adb('shell', 'getprop', 'ro.product.model') != 'Quest 3':
        raise SystemExit('Connect one authorized Quest 3.')
    adb('install', '-r', str(args.apk))
    write_private('umn-key.txt', key)
    if args.route == 'usb':
        adb('reverse', 'tcp:8765', 'tcp:8765')
        write_private('gateway-url.txt', b'http://127.0.0.1:8765')
    else:
        adb('shell', 'run-as ' + PACKAGE + ' rm -f ' + shlex.quote(DATA + '/gateway-url.txt'))
    adb('shell', 'am', 'force-stop', PACKAGE)
    activity = adb('shell', 'cmd', 'package', 'resolve-activity', '--brief', PACKAGE).splitlines()[-1]
    if not activity.startswith(PACKAGE + '/'):
        raise RuntimeError('Could not resolve the FP2 launcher activity.')
    adb('shell', 'am', 'start', '-n', activity)
    print('Installed and launched FP2 Live Captions; route=' + args.route + '. Credential stored in app data, not the APK.')


if __name__ == '__main__':
    main()

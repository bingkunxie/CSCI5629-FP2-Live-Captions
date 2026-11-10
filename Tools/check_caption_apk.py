"""Reject the obsolete A4 shader that was present during the startup ANR.

The identifier comes from ConvGeneric.compute.meta in the removed inference
package. Checking the packaged artifact catches stale incremental-build data.
"""
import argparse
from pathlib import Path
from zipfile import ZipFile

parser = argparse.ArgumentParser()
parser.add_argument('apk', type=Path)
args = parser.parse_args()
with ZipFile(args.apk) as archive:
    bad = [name for name in archive.namelist() if 'a612f75a6b65ab441a0d8045fbfe42f1' in name]
    if bad:
        raise SystemExit('FAIL: obsolete A4 inference shader remains in the APK. Run a clean build.')
    print('PASS: obsolete A4 inference shader is absent.')

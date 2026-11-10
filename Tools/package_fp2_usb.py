"""Finalize the development APK with loopback-only HTTP for the USB fallback.

Unity 6.6 rewrites network_sec_config after the existing A3 callback. Keep
verified HTTPS everywhere except localhost, then package its Gradle project.
"""
from pathlib import Path
import os
import shutil
import subprocess

root = Path(__file__).resolve().parents[1]
gradle = root / 'Library/Bee/Android/Prj/IL2CPP/Gradle'
policy = gradle / 'unityLibrary/src/main/res/xml/network_sec_config.xml'
if not policy.is_file():
    raise SystemExit('Run FP2Setup.BuildBatch first.')
policy.write_text('''<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
 <base-config cleartextTrafficPermitted="false" />
 <domain-config cleartextTrafficPermitted="true">
  <domain includeSubdomains="false">127.0.0.1</domain>
  <domain includeSubdomains="false">localhost</domain>
 </domain-config>
</network-security-config>''')
android = Path('/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/AndroidPlayer')
env = os.environ.copy()
env['JAVA_HOME'] = str(android / 'OpenJDK')
subprocess.run([str(android / 'OpenJDK/bin/java'), '-classpath',
               str(android / 'Tools/gradle/lib/gradle-launcher-9.1.0.jar'),
               'org.gradle.launcher.GradleMain', 'assembleDebug'], cwd=gradle, env=env, check=True)
destination = root / 'build/FP2/FP2-Live-Captions.apk'
shutil.copy2(gradle / 'launcher/build/outputs/apk/debug/launcher-debug.apk', destination)
print('FP2 development APK packaged with loopback-only HTTP fallback.')

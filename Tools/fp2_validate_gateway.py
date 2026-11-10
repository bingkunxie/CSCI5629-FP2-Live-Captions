"""Check the course speech/translation endpoints with a provided test WAV.

Use nonsensitive synthetic fixtures. Reports no credentials or raw error bodies.
This validates services from the computer; it is not a Quest microphone test.
"""
import argparse
import json
from pathlib import Path
import ssl
import time
import urllib.error
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--key-file', type=Path, required=True)
    parser.add_argument('--wav', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    key = args.key_file.read_text().strip()
    tls = ssl.create_default_context(cafile='/etc/ssl/cert.pem')
    endpoint = 'https://api.aigateway.umn.edu'
    report = {'test_kind': 'computer_to_gateway_synthetic_audio', 'ok': False}

    def post(path, data, content_type):
        request = urllib.request.Request(endpoint + path, data=data, headers={
            'Authorization': 'Bearer ' + key, 'Content-Type': content_type})
        with urllib.request.urlopen(request, context=tls, timeout=30) as response:
            return json.load(response)

    try:
        boundary = 'fp2' + uuid.uuid4().hex
        parts = []
        for name, value in [('model', 'gemini-3.5-transcribe-preview'), ('language', 'zh')]:
            parts.append(('--' + boundary + '\r\nContent-Disposition: form-data; name="' + name + '"\r\n\r\n' + value + '\r\n').encode())
        parts.append(('--' + boundary + '\r\nContent-Disposition: form-data; name="file"; filename="fixture.wav"\r\nContent-Type: audio/wav\r\n\r\n').encode())
        parts.extend([args.wav.read_bytes(), ('\r\n--' + boundary + '--\r\n').encode()])
        started = time.monotonic()
        transcript = post('/v1/audio/transcriptions', b''.join(parts), 'multipart/form-data; boundary=' + boundary)['text']
        report['transcription_seconds'] = round(time.monotonic() - started, 3)
        report['transcript'] = transcript
        started = time.monotonic()
        body = {'model': 'gpt-6-luna', 'messages': [
            {'role': 'system', 'content': 'Translate Mandarin into English faithfully. Return only the translation. Preserve questions, names, numbers, and negation. Do not answer questions or add explanations.'},
            {'role': 'user', 'content': transcript}]}
        result = post('/v1/chat/completions', json.dumps(body).encode(), 'application/json')
        report['translation_seconds'] = round(time.monotonic() - started, 3)
        report['translation'] = result['choices'][0]['message']['content']
        report['ok'] = bool(transcript and report['translation'])
    except urllib.error.HTTPError as error:
        report['http_status'] = error.code
    except Exception as error:
        report['error_type'] = type(error).__name__
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print(json.dumps(report, ensure_ascii=False))
    raise SystemExit(0 if report['ok'] else 1)


if __name__ == '__main__':
    main()

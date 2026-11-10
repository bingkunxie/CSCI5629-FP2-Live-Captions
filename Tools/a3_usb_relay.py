"""USB development relay: Quest loopback -> adb reverse -> Mac loopback -> UMN HTTPS.
Start while Cisco UMN VPN is connected. Never listens on the LAN or logs secrets.
"""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import hmac, ssl, urllib.request, urllib.error, time, os
KEY = Path(os.environ.get('UMN_GATEWAY_KEY_FILE', Path(__file__).resolve().parents[1] / '.secrets/umn-key.txt')).read_text().strip()
if not KEY.startswith('sk-'): raise SystemExit('Local gateway key missing')
TLS = ssl.create_default_context(cafile='/etc/ssl/cert.pem')
ALLOWED = {'/v1/audio/transcriptions', '/v1/chat/completions', '/v1/audio/speech'}
class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def do_POST(self):
        if self.path not in ALLOWED: self.send_error(404); return
        if not hmac.compare_digest(self.headers.get('Authorization',''), 'Bearer '+KEY):
            self.send_error(401); return
        try: size=int(self.headers.get('Content-Length','0'))
        except ValueError: self.send_error(400); return
        if not 0 < size <= 12*1024*1024: self.send_error(413); return
        self.connection.settimeout(70)
        data=self.rfile.read(size)
        if len(data)!=size: self.send_error(400); return
        req=urllib.request.Request('https://api.aigateway.umn.edu'+self.path,data=data,headers={
            'Authorization':'Bearer '+KEY,'Content-Type':self.headers.get('Content-Type','application/json')})
        start=time.monotonic()
        try:
            with urllib.request.urlopen(req,context=TLS,timeout=55) as response:
                body=response.read(); status=response.status; content_type=response.headers.get('Content-Type','application/octet-stream')
        except urllib.error.HTTPError as error:
            # Keep server response bodies (which can echo input) out of logs and the client.
            status=error.code; body=b'{"error":{"message":"UMN gateway request rejected; check model access, key and budget."}}'; content_type='application/json'
        except Exception:
            status=502; body=b'{"error":{"message":"UMN gateway unreachable from Mac. Check Cisco VPN."}}';content_type='application/json'
        print(f'{self.path} status={status} elapsed={time.monotonic()-start:.1f}s bytes={len(body)}',flush=True)
        try:
            self.send_response(status);self.send_header('Content-Type',content_type);self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
        except (BrokenPipeError,ConnectionResetError): pass
print('A3 relay listening on 127.0.0.1:8765; keep Cisco VPN and USB connected.',flush=True)
ThreadingHTTPServer(('127.0.0.1',8765),Handler).serve_forever()

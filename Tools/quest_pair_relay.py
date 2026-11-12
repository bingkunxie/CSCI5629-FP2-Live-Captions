"""Two-headset turn coordinator. Standard library only; no course gateway calls."""
import argparse
import asyncio
import hmac
import json
import secrets
import time
from pathlib import Path
from meeting_features import MeetingFeatures

MAX_LINE = 65536
LEASE_SECONDS = 6
TURN_SECONDS = 90

class Room:
    def __init__(self, clock=time.monotonic, agenda_path=None):
        self.clock = clock
        self.clients = {}
        self.owner = self.turn = self.phase = ""
        self.started = 0
        self.revision = 0
        self.meeting = MeetingFeatures(agenda_path, clock)

    def join(self, role, client):
        if role not in ("A", "B") or role in self.clients:
            return False
        self.clients[role] = (client, self.clock())
        return True

    def clear(self):
        self.owner = self.turn = self.phase = ""
        self.revision += 1

    def leave(self, role, client):
        if role in self.clients and self.clients[role][0] is client:
            del self.clients[role]
            if self.owner == role:
                self.clear()

    def expire(self):
        now = self.clock()
        for role, (client, seen) in list(self.clients.items()):
            if now - seen > LEASE_SECONDS:
                self.leave(role, client)
        if self.owner and now - self.started > TURN_SECONDS:
            self.clear()

    def state(self):
        return dict(type="state", owner=self.owner, turn=self.turn, phase=self.phase,
                    revision=self.revision, paired=len(self.clients) == 2)

    def handle(self, role, client, message):
        self.expire()
        if role not in self.clients or self.clients[role][0] is not client:
            return dict(type="error", message="Session expired; reconnect")
        self.clients[role] = (client, self.clock())
        op = message.get("type")
        turn = message.get("turn", "")
        if op in ("reaction", "agenda_get", "agenda_replace", "agenda_set"):
            result = self.meeting.handle(role, message, len(self.clients) == 2)
            return dict(result, eventId=message.get("eventId", "")) if result and result["type"] == "agenda" else result
        if op == "acquire":
            if not isinstance(turn, str) or not 1 <= len(turn) <= 64:
                return dict(type="error", message="Invalid turn")
            if not self.owner and len(self.clients) == 2:
                self.owner, self.turn, self.phase = role, turn, "speaking"
                self.started = self.clock()
                self.revision += 1
            return dict(type="grant", turn=turn, granted=self.owner == role and self.turn == turn)
        if op == "ping":
            return None
        if self.owner != role or turn != self.turn:
            return None  # Old cancellation/results cannot affect the new owner's turn.
        if op == "phase" and message.get("phase") in ("speaking", "processing", "reviewing"):
            self.phase = message["phase"]
            self.revision += 1
        elif op == "release":
            self.clear()
        elif op == "result":
            source, translation = message.get("source"), message.get("translation")
            language = message.get("sourceLanguage")
            if (isinstance(source, str) and isinstance(translation, str) and
                    0 < len(source) <= 8000 and 0 < len(translation) <= 8000 and language in ("zh", "en")):
                result = dict(type="result", owner=role, turn=turn, source=source,
                              translation=translation, sourceLanguage=language)
                self.clear()
                return result
        return None

class Relay:
    def __init__(self, token, agenda_path=None):
        self.token = token
        self.room = Room(agenda_path=agenda_path)
        self.writers = {}

    def send(self, writer, message):
        writer.write((json.dumps(message, ensure_ascii=False) + "\n").encode("utf-8"))

    def broadcast(self, message):
        for writer in list(self.writers):
            try:
                self.send(writer, message)
            except (ConnectionError, RuntimeError):
                writer.close()

    async def connection(self, reader, writer):
        role = None
        try:
            hello = json.loads(await asyncio.wait_for(reader.readline(), 5))
            if (hello.get("type") != "join" or not isinstance(hello.get("token"), str)
                    or not hmac.compare_digest(hello["token"], self.token)):
                self.send(writer, dict(type="error", message="Invalid room token"))
                await writer.drain()
                return
            candidate = hello.get("role")
            if not self.room.join(candidate, writer):
                self.send(writer, dict(type="error", message="Role already connected or invalid"))
                await writer.drain()
                return
            role = candidate
            self.writers[writer] = role
            self.broadcast(self.room.state())
            self.send(writer, self.room.meeting.agenda)
            while True:
                raw = await asyncio.wait_for(reader.readline(), LEASE_SECONDS + 1)
                if not raw:
                    break
                message = json.loads(raw)
                if not isinstance(message, dict):
                    break
                response = self.room.handle(role, writer, message)
                if response:
                    if response["type"] in ("result", "reaction", "agenda"):
                        self.broadcast(response)
                    else:
                        self.send(writer, response)
                        if response["type"] == "notice" and message.get("type", "").startswith("agenda"):
                            self.send(writer, self.room.meeting.agenda)
                    if response["type"] == "error":
                        break
                self.broadcast(self.room.state())
                await asyncio.wait_for(writer.drain(), 2)
        except (ValueError, TypeError, AttributeError, ConnectionError, asyncio.TimeoutError):
            pass
        finally:
            self.writers.pop(writer, None)
            if role:
                self.room.leave(role, writer)
            writer.close()
            self.broadcast(self.room.state())

    async def editor(self, reader, writer):
        # Loopback-only browser editor. Custom header + Host checking prevent cross-site writes.
        try:
            raw = await asyncio.wait_for(reader.readuntil(b"\r\n\r\n"), 5)
            lines = raw.decode("ascii").split("\r\n")
            method, path, _ = lines[0].split()
            headers = dict(line.split(":", 1) for line in lines[1:] if ":" in line)
            headers = {k.lower(): v.strip() for k, v in headers.items()}
            if headers.get("host") not in ("127.0.0.1:" + str(self.editor_port), "localhost:" + str(self.editor_port)):
                raise ValueError("Invalid host")
            if method == "GET" and path == "/":
                body = Path(__file__).with_name("meeting_editor.html").read_bytes()
                mime = "text/html; charset=utf-8"
            elif method == "GET" and path == "/agenda":
                body = json.dumps(self.room.meeting.agenda, ensure_ascii=False).encode()
                mime = "application/json"
            elif method == "POST" and path == "/agenda" and headers.get("x-fp2-editor") == "1":
                length = int(headers.get("content-length", "0"))
                if not 0 < length <= 16384:
                    raise ValueError("Invalid size")
                message = json.loads(await asyncio.wait_for(reader.readexactly(length), 5))
                if message.get("type") not in ("agenda_replace", "agenda_set"):
                    raise ValueError("Invalid operation")
                result = self.room.meeting.handle("host", message)
                if result["type"] == "agenda":
                    self.broadcast(result)
                body = json.dumps(result, ensure_ascii=False).encode()
                mime = "application/json"
            else:
                raise ValueError("Invalid request")
            writer.write(("HTTP/1.1 200 OK\r\nContent-Type: " + mime +
                          "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Length: " +
                          str(len(body)) + "\r\nConnection: close\r\n\r\n").encode() + body)
            await writer.drain()
        except (ValueError, KeyError, TypeError, AttributeError, OSError, asyncio.IncompleteReadError, asyncio.LimitOverrunError, asyncio.TimeoutError):
            writer.write(b"HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
        finally:
            writer.close()

    async def tick(self):
        while True:
            await asyncio.sleep(1)
            self.room.expire()
            for writer, role in list(self.writers.items()):
                if role not in self.room.clients or self.room.clients[role][0] is not writer:
                    writer.close()
                    self.writers.pop(writer, None)
                elif writer.transport.get_write_buffer_size() > MAX_LINE * 2:
                    writer.close()
            self.broadcast(self.room.state())

async def run(args):
    token_path = args.config_dir / "room-token.txt"
    args.config_dir.mkdir(parents=True, exist_ok=True)
    if not token_path.exists():
        token_path.write_text(secrets.token_urlsafe(24), encoding="utf-8")
    token = token_path.read_text(encoding="utf-8").strip()
    if len(token) < 20:
        raise SystemExit("Room token must have at least 20 characters.")
    for role in ("A", "B"):
        (args.config_dir / ("pair-" + role + ".json")).write_text(json.dumps(
            dict(host=args.host, port=args.port, role=role, token=token)), encoding="utf-8")
    relay = Relay(token, args.config_dir / "meeting-agenda.json")
    relay.editor_port = args.editor_port
    server = await asyncio.start_server(relay.connection, args.bind, args.port, limit=MAX_LINE)
    print(f"Quest pair relay listening on {args.bind}:{args.port}. Device configs in {args.config_dir}", flush=True)
    editor = await asyncio.start_server(relay.editor, "127.0.0.1", args.editor_port, limit=16384)
    print(f"Offline agenda editor: http://127.0.0.1:{args.editor_port}", flush=True)
    tick = asyncio.create_task(relay.tick())
    try:
        async with server, editor:
            await server.serve_forever()
    finally:
        tick.cancel()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", required=True, help="Laptop LAN IPv4 address reachable by both headsets")
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8766)
    parser.add_argument("--editor-port", type=int, default=8767)
    parser.add_argument("--config-dir", required=True, type=Path, help="Private directory outside repository")
    try:
        asyncio.run(run(parser.parse_args()))
    except KeyboardInterrupt:
        pass

"""Offline shared agenda and ephemeral reactions; no speech/gateway dependencies."""
import json
import os
import time
import uuid
from collections import deque
from pathlib import Path

KINDS = ("like", "clap", "love", "question")

def validate_items(items):
    if not isinstance(items, list) or len(items) > 6:
        raise ValueError("Use at most 6 agenda items.")
    texts = []
    for item in items:
        text = item.get("text") if isinstance(item, dict) else None
        if not isinstance(text, str) or not text.strip() or "\n" in text or "\r" in text or len(text.encode("utf-16-le")) // 2 > 60:
            raise ValueError("Each agenda item needs 1-60 characters on one line.")
        texts.append(text.strip())
    return texts

class MeetingFeatures:
    def __init__(self, path=None, clock=time.monotonic):
        self.path = Path(path) if path else None
        self.clock = clock
        self.last_reaction = {}
        self.events = deque(maxlen=128)
        self.agenda = dict(type="agenda", meetingId=uuid.uuid4().hex, agendaVersion=0, items=[])
        if self.path and self.path.exists():
            value = json.loads(self.path.read_text(encoding="utf-8"))
            validate_items(value["items"])
            ids = [x.get("id") for x in value["items"]]
            if (not isinstance(value.get("meetingId"), str) or not isinstance(value.get("agendaVersion"), int)
                    or value["agendaVersion"] < 0 or len(set(ids)) != len(ids)
                    or any(not isinstance(x, str) or not x for x in ids)
                    or any(type(x.get("done")) is not bool for x in value["items"])):
                raise ValueError("Invalid saved agenda; retain and inspect the file.")
            value["type"] = "agenda"
            self.agenda = value

    def commit(self, items):
        value = dict(type="agenda", meetingId=self.agenda["meetingId"],
                     agendaVersion=self.agenda["agendaVersion"] + 1, items=items)
        if self.path:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temporary = self.path.with_suffix(".tmp")
            with temporary.open("w", encoding="utf-8") as f:
                json.dump(value, f, ensure_ascii=False)
                f.flush()
                os.fsync(f.fileno())
            os.replace(temporary, self.path)
        self.agenda = value
        return value

    def handle(self, role, message, paired=True):
        op = message.get("type")
        try:
            if op == "reaction":
                kind, event = message.get("reaction"), message.get("eventId")
                if kind not in KINDS or not isinstance(event, str) or not 1 <= len(event) <= 64:
                    return dict(type="notice", message="Unknown reaction.")
                if not paired:
                    return dict(type="notice", message="Wait for the other headset before sending.")
                if event in self.events:
                    return None
                if self.clock() - self.last_reaction.get(role, float("-inf")) < 2:
                    return dict(type="notice", message="Wait a moment before sending another reaction.")
                self.last_reaction[role] = self.clock()
                self.events.append(event)
                return dict(type="reaction", owner=role, reaction=kind, eventId=event)
            if op == "agenda_get":
                return self.agenda
            if op == "agenda_replace":
                if message.get("agendaVersion") != self.agenda["agendaVersion"]:
                    return dict(type="notice", message="Agenda changed elsewhere. Close and reopen the editor before saving.")
                texts = validate_items(message.get("items"))
                old = list(self.agenda["items"])
                items = []
                for text in texts:
                    match = next((x for x in old if x["text"] == text), None)
                    if match:
                        old.remove(match)
                    items.append(dict(id=match["id"] if match else uuid.uuid4().hex,
                                      text=text, done=match["done"] if match else False))
                return self.commit(items)
            if op == "agenda_set":
                if type(message.get("done")) is not bool:
                    raise ValueError("Invalid completion state.")
                if not any(x["id"] == message.get("itemId") for x in self.agenda["items"]):
                    raise ValueError("Agenda item changed. Refresh and try again.")
                items = [dict(x, done=message["done"]) if x["id"] == message["itemId"] else dict(x) for x in self.agenda["items"]]
                if items == self.agenda["items"]:
                    return self.agenda
                return self.commit(items)
            return None
        except ValueError as error:
            return dict(type="notice", message=str(error))
        except OSError:
            return dict(type="notice", message="Could not save agenda; previous agenda retained.")

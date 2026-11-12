import json,tempfile,unittest,asyncio
from pathlib import Path
from meeting_features import MeetingFeatures
from quest_pair_relay import Room,Relay
class MeetingTests(unittest.TestCase):
    def setUp(self):
        self.now=0
        self.room=Room(lambda:self.now)
        self.a,self.b=object(),object()
        self.room.join("A",self.a);self.room.join("B",self.b)
    def send(self,role,**m):return self.room.handle(role,self.a if role=="A" else self.b,m)
    def test_reaction_does_not_change_speaking_lock(self):
        self.send("A",type="acquire",turn="speech")
        r=self.send("B",type="reaction",reaction="like",eventId="one",owner="A")
        self.assertEqual(r["owner"],"B");self.assertEqual(self.room.owner,"A");self.assertEqual(self.room.turn,"speech")
        self.assertIsNone(self.send("B",type="reaction",reaction="like",eventId="one"))
        self.assertEqual(self.send("B",type="reaction",reaction="love",eventId="two")["type"],"notice")
        self.now=3
        self.assertEqual(self.send("B",type="reaction",reaction="love",eventId="two")["type"],"reaction")
    def test_unknown_and_absent_peer_reactions_rejected(self):
        self.assertEqual(self.send("A",type="reaction",reaction="evil",eventId="x")["type"],"notice")
        self.room.leave("B",self.b)
        self.assertEqual(self.send("A",type="reaction",reaction="like",eventId="x")["type"],"notice")
    def test_agenda_updates_preserve_lock_and_merge_distinct_checks(self):
        self.send("A",type="acquire",turn="speech")
        a=self.send("B",type="agenda_replace",agendaVersion=0,items=[{"text":"目标"},{"text":"Next"}],eventId="save")
        self.assertEqual(a["eventId"],"save")
        self.send("A",type="agenda_set",itemId=a["items"][0]["id"],done=True)
        b=self.send("B",type="agenda_set",itemId=a["items"][1]["id"],done=True)
        self.assertTrue(all(x["done"] for x in b["items"]));self.assertEqual(self.room.owner,"A")
        stale=self.send("A",type="agenda_replace",agendaVersion=0,items=[])
        self.assertEqual(stale["type"],"notice");self.assertEqual(len(self.room.meeting.agenda["items"]),2)
    def test_persistence_and_reconnect_keep_agenda_not_reactions(self):
        with tempfile.TemporaryDirectory(dir=Path.cwd()) as d:
            path=Path(d)/"agenda.json";features=MeetingFeatures(path)
            a=features.handle("A",dict(type="agenda_replace",agendaVersion=0,items=[{"text":"会议要点"}]))
            features.handle("B",dict(type="agenda_set",itemId=a["items"][0]["id"],done=True))
            loaded=MeetingFeatures(path)
            self.assertTrue(loaded.agenda["items"][0]["done"]);self.assertEqual(len(loaded.events),0)
    def test_limits_and_invalid_completion_leave_previous_snapshot(self):
        features=MeetingFeatures()
        for items in ([{"text":"x"}]*7,[{"text":"x"*61}],[{"text":"a\nb"}]):
            self.assertEqual(features.handle("A",dict(type="agenda_replace",agendaVersion=0,items=items))["type"],"notice")
        self.assertEqual(features.agenda["agendaVersion"],0)
    def test_storage_failure_retains_previous_snapshot(self):
        with tempfile.TemporaryDirectory(dir=Path.cwd()) as d:
            path=Path(d)/"blocker";path.write_text("file")
            features=MeetingFeatures()
            features.path=path/"agenda.json"
            self.assertEqual(features.handle("A",dict(type="agenda_replace",agendaVersion=0,items=[{"text":"x"}]))["type"],"notice")
            self.assertEqual(features.agenda["items"],[])

class EditorTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.relay=Relay("test-token")
        self.server=await asyncio.start_server(self.relay.editor,"127.0.0.1",0)
        self.port=self.server.sockets[0].getsockname()[1];self.relay.editor_port=self.port
    async def asyncTearDown(self):
        self.server.close();await self.server.wait_closed()
    async def request(self,body,custom=True,host=None):
        r,w=await asyncio.open_connection("127.0.0.1",self.port)
        b=json.dumps(body).encode()
        headers=f"POST /agenda HTTP/1.1\r\nHost: {host or '127.0.0.1:'+str(self.port)}\r\nContent-Length: {len(b)}\r\n"
        if custom:headers+="X-FP2-Editor: 1\r\n"
        w.write((headers+"\r\n").encode()+b);await w.drain()
        data=await r.read();w.close();await w.wait_closed();return data
    async def test_loopback_editor_saves_unicode_and_refuses_cross_site_writes(self):
        payload=dict(type="agenda_replace",agendaVersion=0,items=[{"text":"讨论计划"}])
        data=await self.request(payload);self.assertIn(b"200 OK",data)
        self.assertEqual(self.relay.room.meeting.agenda["items"][0]["text"],"讨论计划")
        self.assertIn(b"400 Bad Request",await self.request(payload,custom=False))
        self.assertIn(b"400 Bad Request",await self.request(payload,host="untrusted.example"))
if __name__=="__main__":unittest.main(verbosity=2)

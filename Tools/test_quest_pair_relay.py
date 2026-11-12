import asyncio
import json
import unittest
from quest_pair_relay import Room, Relay

class RoomTests(unittest.TestCase):
    def setUp(self):
        self.now = 0
        self.room = Room(lambda: self.now)
        self.a, self.b = object(), object()
        self.room.join("A", self.a)
        self.room.join("B", self.b)
    def send(self, role, **m):
        return self.room.handle(role, self.a if role == "A" else self.b, m)
    def test_first_speaker_wins(self):
        self.assertTrue(self.send("A",type="acquire",turn="one")["granted"])
        self.assertFalse(self.send("B",type="acquire",turn="two")["granted"])
    def test_duplicate_role_rejected(self):
        self.assertFalse(self.room.join("A",object()))
    def test_stale_release_and_non_owner_cannot_end_turn(self):
        self.send("A",type="acquire",turn="one")
        self.send("A",type="release",turn="old")
        self.send("B",type="release",turn="one")
        self.assertEqual(self.room.owner,"A")
    def test_phase_and_result_preserve_unicode(self):
        self.send("A",type="acquire",turn="one")
        self.send("A",type="phase",turn="one",phase="processing")
        self.assertEqual(self.room.phase,"processing")
        result=self.send("A",type="result",turn="one",source="你好",translation="Hello",sourceLanguage="zh")
        self.assertEqual(result["source"],"你好")
        self.assertEqual(self.room.owner,"")
        self.assertTrue(self.send("B",type="acquire",turn="two")["granted"])
    def test_expired_owner_releases_lock(self):
        self.send("A",type="acquire",turn="one")
        self.now=5
        self.send("B",type="ping")
        self.now=7
        self.room.expire()
        self.assertEqual(self.room.owner,"")
        self.assertNotIn("A",self.room.clients)
    def test_hard_deadline_even_with_heartbeats(self):
        self.send("A",type="acquire",turn="one")
        for t in range(1,92):
            self.now=t
            self.send("A",type="ping")
            self.send("B",type="ping")
        self.assertEqual(self.room.owner,"")
    def test_old_socket_cannot_remove_replacement(self):
        self.send("A",type="acquire",turn="one")
        self.room.leave("A",self.a)
        replacement=object()
        self.assertTrue(self.room.join("A",replacement))
        self.room.leave("A",self.a)
        self.assertIs(self.room.clients["A"][0],replacement)
    def test_absent_partner_cannot_start(self):
        self.room.leave("B",self.b)
        self.assertFalse(self.send("A",type="acquire",turn="one")["granted"])
    def test_oversized_result_not_published(self):
        self.send("A",type="acquire",turn="one")
        self.assertIsNone(self.send("A",type="result",turn="one",source="x"*8001,translation="a",sourceLanguage="en"))
        self.assertEqual(self.room.owner,"A")

class NetworkTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.relay=Relay("test-room-token-not-a-course-key")
        self.server=await asyncio.start_server(self.relay.connection,"127.0.0.1",0)
        self.port=self.server.sockets[0].getsockname()[1]
        self.connections=[]
    async def asyncTearDown(self):
        for _,w in self.connections:
            w.close()
            await w.wait_closed()
        self.server.close()
        await self.server.wait_closed()
        await asyncio.sleep(.03)
    async def connect(self,role,token="test-room-token-not-a-course-key"):
        r,w=await asyncio.open_connection("127.0.0.1",self.port)
        self.connections.append((r,w))
        w.write((json.dumps(dict(type="join",role=role,token=token))+"\n").encode())
        await w.drain()
        return r,w
    async def receive(self,r,kind,predicate=lambda _:True):
        async def read():
            while True:
                line=await r.readline()
                if not line: raise AssertionError("Unexpected disconnect")
                value=json.loads(line)
                if value["type"]==kind and predicate(value): return value
        return await asyncio.wait_for(read(),2)
    async def test_real_sockets_contend_and_forward_unicode_result(self):
        ar,aw=await self.connect("A")
        br,bw=await self.connect("B")
        await self.receive(ar,"state",lambda m:m["paired"])
        await self.receive(br,"state",lambda m:m["paired"])
        aw.write(b'{"type":"acquire","turn":"a1"}\n')
        bw.write(b'{"type":"acquire","turn":"b1"}\n')
        await aw.drain()
        await bw.drain()
        ga=await self.receive(ar,"grant")
        gb=await self.receive(br,"grant")
        self.assertNotEqual(ga["granted"],gb["granted"])
        w,other,turn=(aw,br,"a1") if ga["granted"] else (bw,ar,"b1")
        w.write((json.dumps(dict(type="phase",turn=turn,phase="reviewing"))+"\n").encode())
        await w.drain()
        await self.receive(other,"state",lambda m:m["phase"]=="reviewing")
        w.write((json.dumps(dict(type="result",turn=turn,source="你好",translation="Hello",sourceLanguage="zh"))+"\n").encode())
        await w.drain()
        result=await self.receive(other,"result")
        self.assertEqual(result["source"],"你好")
        await self.receive(other,"state",lambda m:not m["owner"])
    async def test_wrong_token_and_duplicate_role_rejected(self):
        r,_=await self.connect("A","incorrect")
        self.assertEqual((await self.receive(r,"error"))["message"],"Invalid room token")
        first,_=await self.connect("A")
        await self.receive(first,"state")
        second,_=await self.connect("A")
        self.assertIn("Role",(await self.receive(second,"error"))["message"])
    async def test_disconnect_unlocks_peer(self):
        ar,aw=await self.connect("A")
        br,_=await self.connect("B")
        await self.receive(ar,"state",lambda m:m["paired"])
        aw.write(b'{"type":"acquire","turn":"a1"}\n')
        await aw.drain()
        await self.receive(br,"state",lambda m:m["owner"]=="A")
        aw.close()
        await aw.wait_closed()
        result=await self.receive(br,"state",lambda m:not m["paired"])
        self.assertEqual(result["owner"],"")

if __name__=="__main__": unittest.main(verbosity=2)

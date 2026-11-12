"""Offline stand-in for one headset, for checking waiting messages on the other."""
import argparse
import asyncio
import json
from pathlib import Path

async def run(args):
    config = json.loads(args.config.read_text(encoding="utf-8-sig"))
    reader, writer = await asyncio.open_connection(config["host"], config["port"])
    async def send(**message):
        writer.write((json.dumps(message) + "\n").encode())
        await writer.drain()
    await send(type="join",role=config["role"],token=config["token"])
    async def heartbeat():
        while True:
            await send(type="ping")
            await asyncio.sleep(1)
    task=asyncio.create_task(heartbeat())
    turn="offline-status-probe"
    try:
        print("Waiting for the other headset; no microphone audio or gateway calls.",flush=True)
        while True:
            raw=await reader.readline()
            if not raw: raise RuntimeError("Relay disconnected")
            message=json.loads(raw)
            if message["type"]=="error": raise RuntimeError(message["message"])
            if message["type"]=="state" and message["paired"] and not message["owner"]: break
        await send(type="acquire",turn=turn)
        while True:
            message=json.loads(await reader.readline())
            if message["type"]=="grant":
                if not message["granted"]: raise RuntimeError("Other headset owns the turn")
                break
        for phase in ("speaking","processing","reviewing"):
            await send(type="phase",turn=turn,phase=phase)
            print("Simulated peer state: "+phase,flush=True)
            await asyncio.sleep(args.seconds)
        await send(type="release",turn=turn)
        print("Turn released. No synthetic conversation was saved.",flush=True)
    finally:
        task.cancel()
        writer.close()
        await writer.wait_closed()

if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config",required=True,type=Path)
    parser.add_argument("--seconds",type=float,default=8)
    asyncio.run(run(parser.parse_args()))

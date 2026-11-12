"""Prepare or install an offline meeting agenda on a standalone Quest."""
import argparse,json,subprocess,uuid
from pathlib import Path
from meeting_features import validate_items

def load(path):
    text=path.read_text(encoding="utf-8-sig")
    if path.suffix.lower()==".json":
        value=json.loads(text)
        validate_items(value["items"])
        value.setdefault("meetingId",uuid.uuid4().hex)
        value.setdefault("agendaVersion",0)
        for item in value["items"]:
            item.setdefault("id",uuid.uuid4().hex)
            item.setdefault("done",False)
    else:
        items=[dict(id=uuid.uuid4().hex,text=line.strip(),done=False) for line in text.splitlines() if line.strip()]
        validate_items(items)
        value=dict(meetingId=uuid.uuid4().hex,agendaVersion=0,items=items)
    ids=[item["id"] for item in value["items"]]
    if (not isinstance(value["meetingId"],str) or not 1<=len(value["meetingId"])<=64
            or type(value["agendaVersion"]) is not int or value["agendaVersion"]<0
            or any(not isinstance(i,str) or not 1<=len(i)<=64 for i in ids) or len(set(ids))!=len(ids)
            or any(type(item["done"]) is not bool for item in value["items"])):
        raise ValueError("Invalid agenda metadata.")
    return value

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--file",type=Path,required=True,help="UTF-8 text (one item per line), or exported JSON")
    parser.add_argument("--adb",default="adb")
    parser.add_argument("--serial",required=True)
    args=parser.parse_args()
    value=load(args.file)
    data=json.dumps(value,ensure_ascii=False).encode("utf-8")
    command="run-as edu.umn.csci5629.livecaptions sh -c 'umask 077; mkdir -p files; cat > files/meeting-agenda-import.json'"
    result=subprocess.run([args.adb,"-s",args.serial,"shell",command],input=data,capture_output=True)
    if result.returncode:raise SystemExit("Install failed; check USB debugging and the development APK.")
    print("Agenda prepared:",len(value["items"]),"items. Restart the app in single-headset mode to import.")
if __name__=="__main__":main()

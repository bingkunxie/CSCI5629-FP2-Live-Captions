using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FP2.Captions
{
    [Serializable] public sealed class AgendaItem
    {
        public string id, text;
        public bool done;
    }
    [Serializable] public sealed class AgendaSnapshot
    {
        public string meetingId;
        public int agendaVersion;
        public AgendaItem[] items = Array.Empty<AgendaItem>();
    }
    public static class MeetingAgenda
    {
        public const int MaxItems = 6, MaxLength = 60;
        public static void Validate(AgendaSnapshot agenda)
        {
            if (agenda == null || string.IsNullOrEmpty(agenda.meetingId) || agenda.meetingId.Length > 64 ||
                agenda.agendaVersion < 0 || agenda.items == null || agenda.items.Length > MaxItems)
                throw new ArgumentException("Invalid agenda.");
            var ids = new HashSet<string>();
            foreach (var item in agenda.items)
                if (item == null || string.IsNullOrWhiteSpace(item.id) || item.id.Length > 64 || !ids.Add(item.id) ||
                    string.IsNullOrWhiteSpace(item.text) || item.text.Length > MaxLength || item.text.Contains("\n") || item.text.Contains("\r"))
                    throw new ArgumentException("Use up to 6 agenda items, each at most 60 characters.");
        }
        public static AgendaSnapshot Empty() => new AgendaSnapshot { meetingId=Guid.NewGuid().ToString("N") };
        public static AgendaSnapshot Replace(AgendaSnapshot previous, IEnumerable<string> lines)
        {
            Validate(previous);
            var available = previous.items.ToList();
            var result = new List<AgendaItem>();
            foreach (string raw in lines)
            {
                string text = (raw ?? "").Trim();
                if (text.Length == 0) continue;
                var old = available.FirstOrDefault(x => x.text == text);
                if (old != null) available.Remove(old);
                result.Add(new AgendaItem { id=old?.id ?? Guid.NewGuid().ToString("N"), text=text, done=old?.done ?? false });
            }
            var next = new AgendaSnapshot { meetingId=previous.meetingId, agendaVersion=previous.agendaVersion+1, items=result.ToArray() };
            Validate(next); return next;
        }
        public static AgendaSnapshot SetDone(AgendaSnapshot previous, string id, bool done)
        {
            Validate(previous);
            if (!previous.items.Any(x => x.id == id)) throw new ArgumentException("Agenda item no longer exists.");
            return new AgendaSnapshot { meetingId=previous.meetingId, agendaVersion=previous.agendaVersion+1,
                items=previous.items.Select(x => new AgendaItem { id=x.id,text=x.text,done=x.id==id ? done : x.done }).ToArray() };
        }
    }

    public sealed class AgendaStore
    {
        private readonly string path;
        public AgendaStore(string path) { this.path=path; }
        public AgendaSnapshot Load()
        {
            if (!File.Exists(path)) return MeetingAgenda.Empty();
            try { return Read(path); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is FormatException)
            { if (File.Exists(path+".bak")) return Read(path+".bak"); throw; }
        }
        private static AgendaSnapshot Read(string path)
        {
            using var reader=new BinaryReader(File.OpenRead(path));
            if (reader.ReadString()!="FP2-AGENDA-1") throw new InvalidDataException();
            var result=new AgendaSnapshot { meetingId=reader.ReadString(),agendaVersion=reader.ReadInt32() };
            int count=reader.ReadInt32();
            if (count<0 || count>MeetingAgenda.MaxItems) throw new InvalidDataException();
            result.items=new AgendaItem[count];
            for(int i=0;i<count;i++) result.items[i]=new AgendaItem {id=reader.ReadString(),text=reader.ReadString(),done=reader.ReadBoolean()};
            MeetingAgenda.Validate(result); return result;
        }
        public void Save(AgendaSnapshot agenda)
        {
            MeetingAgenda.Validate(agenda);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
            {
                using var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true);
                writer.Write("FP2-AGENDA-1"); writer.Write(agenda.meetingId); writer.Write(agenda.agendaVersion); writer.Write(agenda.items.Length);
                foreach(var item in agenda.items) {writer.Write(item.id);writer.Write(item.text);writer.Write(item.done);}
                writer.Flush();stream.Flush(true);
            }
            if(File.Exists(path)) File.Replace(path+".tmp",path,path+".bak");
            else File.Move(path+".tmp",path);
        }
    }

    public sealed class ReactionGate
    {
        public const double Lifetime=2.5, Cooldown=2;
        public static readonly string[] Kinds={"like","clap","love","question"};
        private readonly HashSet<string> seen=new HashSet<string>();
        private readonly Queue<string> order=new Queue<string>();
        private double lastSent=double.NegativeInfinity;
        public bool TrySend(string kind,double now)
        {
            if (!Kinds.Contains(kind) || now-lastSent<Cooldown) return false;
            lastSent=now;return true;
        }
        public bool Accept(string id,string kind,double receivedAt,double now)
        {
            if(string.IsNullOrWhiteSpace(id)||id.Length>64||!Kinds.Contains(kind)||now-receivedAt>3||now<receivedAt||!seen.Add(id))return false;
            order.Enqueue(id);if(order.Count>128)seen.Remove(order.Dequeue());return true;
        }
    }
}

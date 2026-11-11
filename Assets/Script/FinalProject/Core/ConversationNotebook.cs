using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FP2.Captions
{
    public sealed class ConversationEntry
    {
        public string Id, UtcTime, Speaker, Source, Translation, Direction;
        public bool Reviewed;
        public double ProcessingSeconds;
    }

    // Atomic snapshots stay in the application's private directory. Audio is never stored.
    public sealed class ConversationNotebook
    {
        private readonly string path;
        private List<ConversationEntry> entries = new List<ConversationEntry>();
        public IReadOnlyList<ConversationEntry> Entries => entries.AsReadOnly();
        public bool RecoveredFromBackup { get; private set; }
        public const int Capacity = 500;
        public ConversationNotebook(string path)
        {
            this.path = path;
            if (!File.Exists(path)) return;
            try { entries = Read(path); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is FormatException)
            {
                if (!File.Exists(path + ".bak")) throw;
                entries = Read(path + ".bak"); RecoveredFromBackup = true;
            }
        }
        private static List<ConversationEntry> Read(string file)
        {
            using var reader = new BinaryReader(File.OpenRead(file), Encoding.UTF8);
            if (reader.ReadString() != "FP2-NOTEBOOK-1") throw new InvalidDataException("Unknown notebook format.");
            int count = reader.ReadInt32();
            if (count < 0 || count > Capacity) throw new InvalidDataException("Invalid notebook size.");
            var result = new List<ConversationEntry>();
            for (int i = 0; i < count; i++)
                result.Add(new ConversationEntry { Id=reader.ReadString(), UtcTime=reader.ReadString(), Speaker=reader.ReadString(),
                    Direction=reader.ReadString(), Source=reader.ReadString(), Translation=reader.ReadString(),
                    Reviewed=reader.ReadBoolean(), ProcessingSeconds=reader.ReadDouble() });
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected notebook data.");
            return result;
        }
        public bool Add(ConversationEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Source) ||
                string.IsNullOrWhiteSpace(entry.Translation)) throw new ArgumentException("Incomplete conversation turn.");
            if (entries.Exists(e => e.Id == entry.Id)) return false;
            var next = new List<ConversationEntry>(entries) { entry };
            if (next.Count > Capacity) next.RemoveAt(0);
            Save(next); entries = next; return true;
        }
        public void Clear()
        {
            Save(new List<ConversationEntry>()); entries.Clear();
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
        private void Save(List<ConversationEntry> next)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                writer.Write("FP2-NOTEBOOK-1"); writer.Write(next.Count);
                foreach (var e in next)
                {
                    writer.Write(e.Id); writer.Write(e.UtcTime ?? ""); writer.Write(e.Speaker ?? "");
                    writer.Write(e.Direction ?? ""); writer.Write(e.Source); writer.Write(e.Translation);
                    writer.Write(e.Reviewed); writer.Write(e.ProcessingSeconds);
                }
                writer.Flush(); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        public string ToMarkdown()
        {
            var text = new StringBuilder("# Conversation notebook\n\nTimes use UTC. Speaker labels are selected manually.\n\n");
            foreach (var e in entries)
            {
                text.Append("## ").Append(e.UtcTime).Append(" / ").Append(e.Speaker).Append('\n');
                text.Append("\n").Append(e.Direction).Append(e.Reviewed ? " / User confirmed\n\n" : " / Automatic\n\n");
                text.Append("Original\n\n").Append(Quote(e.Source)).Append("\n\nTranslation\n\n").Append(Quote(e.Translation)).Append("\n\n");
                text.Append("Processing time ").Append(e.ProcessingSeconds.ToString("F2", CultureInfo.InvariantCulture)).Append(" s\n\n");
            }
            return text.ToString();
        }
        private static string Quote(string value) => "> " + value.Replace("\r", "").Replace("\n", "\n> ");
    }
}

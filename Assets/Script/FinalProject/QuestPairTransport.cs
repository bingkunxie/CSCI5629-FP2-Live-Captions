using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Optional trusted-LAN transport. Never sends the course key or microphone audio.
[Serializable] public sealed class PairConfig { public string host, role, token; public int port = 8766; }
[Serializable] public sealed class PairPacket
{
    public string type, token, role, owner, turn, phase, source, translation, sourceLanguage, message;
    public bool granted, paired;
    public int revision;
}
public sealed class QuestPairTransport : IDisposable
{
    public readonly ConcurrentQueue<PairPacket> Received = new ConcurrentQueue<PairPacket>();
    private readonly ConcurrentQueue<string> outbound = new ConcurrentQueue<string>();
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    private TcpClient socket;
    private readonly PairConfig config;
    public QuestPairTransport(PairConfig config)
    {
        this.config = config;
        _ = Task.Run(Run);
    }
    public void Send(PairPacket packet) => outbound.Enqueue(JsonUtility.ToJson(packet));
    private void Run()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                socket = client;
                if (!client.ConnectAsync(config.host, config.port).Wait(3000)) throw new IOException();
                client.NoDelay = true;
                using var stream = client.GetStream();
                stream.ReadTimeout = 2000; stream.WriteTimeout = 2000;
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
                writer.WriteLine(JsonUtility.ToJson(new PairPacket { type="join", role=config.role, token=config.token }));
                var line = new StringBuilder();
                var buffer = new byte[8192];
                var decoder = Encoding.UTF8.GetDecoder();
                var chars = new char[8192];
                DateTime nextPing = DateTime.UtcNow, lastMessage = DateTime.UtcNow;
                while (!stopping.IsCancellationRequested)
                {
                    while (outbound.TryDequeue(out string message)) writer.WriteLine(message);
                    if (DateTime.UtcNow >= nextPing)
                    {
                        writer.WriteLine("{\"type\":\"ping\"}");
                        nextPing = DateTime.UtcNow.AddSeconds(1);
                    }
                    if (client.Client.Poll(50000, SelectMode.SelectRead))
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if (read == 0) throw new IOException();
                        int count = decoder.GetChars(buffer, 0, read, chars, 0);
                        for (int i = 0; i < count; i++)
                        {
                            if (chars[i] == '\n')
                            {
                                var packet = JsonUtility.FromJson<PairPacket>(line.ToString());
                                line.Clear(); lastMessage = DateTime.UtcNow;
                                if (packet != null) Received.Enqueue(packet);
                            }
                            else line.Append(chars[i]);
                            if (line.Length > 65536) throw new IOException();
                        }
                    }
                    if ((DateTime.UtcNow - lastMessage).TotalSeconds > 4) throw new IOException();
                }
            }
            catch (Exception) { if (!stopping.IsCancellationRequested) Received.Enqueue(new PairPacket { type="disconnected" }); }
            finally
            {
                socket = null;
                while (outbound.TryDequeue(out _)) { }
            }
            stopping.Token.WaitHandle.WaitOne(1000);
        }
    }
    public void Dispose() { stopping.Cancel(); socket?.Close(); }
}

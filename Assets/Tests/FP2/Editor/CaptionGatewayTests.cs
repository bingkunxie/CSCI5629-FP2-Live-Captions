using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FP2.Captions;
using NUnit.Framework;
using UnityEngine;

public class CaptionGatewayTests
{
    [Serializable] private class Message { public string role; public string content; }
    [Serializable] private class ChatRequest { public string model; public Message[] messages; }

    private static HttpListener Listen(out string endpoint)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        endpoint = "http://127.0.0.1:" + port;
        var listener = new HttpListener(); listener.Prefixes.Add(endpoint + "/"); listener.Start();
        return listener;
    }

    private static ICaptionGateway Gateway(string endpoint)
    {
        Type type = Type.GetType("MandarinCaptionGateway, Assembly-CSharp");
        Assert.That(type, Is.Not.Null);
        return (ICaptionGateway)Activator.CreateInstance(type, endpoint, "local-test-key");
    }

    private static async Task Respond(HttpListenerContext context, string body, int code = 200)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = code;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    [Test]
    public async Task ActualAdapterSendsMandarinTranscriptionAndTranslatesTheRecognizedUtterance()
    {
        using var listener = Listen(out string endpoint);
        string multipart = null, path = null, chatJson = null;
        var serve = Task.Run(async () => {
            var first = await listener.GetContextAsync();
            path = first.Request.Url.AbsolutePath;
            using (var reader = new StreamReader(first.Request.InputStream)) multipart = await reader.ReadToEndAsync();
            await Respond(first, "{\"text\":\"今天的会议几点开始？\"}");
            var second = await listener.GetContextAsync();
            using (var reader = new StreamReader(second.Request.InputStream)) chatJson = await reader.ReadToEndAsync();
            await Respond(second, "{\"choices\":[{\"message\":{\"content\":\"What time does today's meeting start?\"}}]}");
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var gateway = Gateway(endpoint);
        string transcript = await gateway.TranscribeAsync(new byte[] { 82, 73, 70, 70 }, timeout.Token);
        string translation = await gateway.TranslateAsync(transcript, timeout.Token);
        await serve;
        Assert.That(path, Is.EqualTo("/v1/audio/transcriptions"));
        string language = Regex.Match(multipart, "name=\"language\"[^\\r\\n]*\\r\\n(?:[^\\r\\n]+\\r\\n)*\\r\\n([^\\r\\n]*)").Groups[1].Value;
        Assert.That(language, Is.EqualTo("zh"));
        Assert.That(multipart, Does.Contain("gemini-3.5-transcribe-preview"));
        var request = JsonUtility.FromJson<ChatRequest>(chatJson);
        Assert.That(request.model, Is.EqualTo("gpt-6-luna"));
        Assert.That(request.messages.Length, Is.EqualTo(2));
        Assert.That(request.messages[0].role, Is.EqualTo("system"));
        Assert.That(request.messages[1].role, Is.EqualTo("user"));
        Assert.That(request.messages[1].content, Is.EqualTo("今天的会议几点开始？"));
        Assert.That(translation, Is.EqualTo("What time does today's meeting start?"));
    }

    [Test]
    public async Task UnauthorizedGatewayResponseBecomesSafeAccessError()
    {
        using var listener = Listen(out string endpoint);
        var serve = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            await Respond(context, "{\"error\":{\"message\":\"sensitive-provider-body\"}}", 401);
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await Gateway(endpoint).TranslateAsync("你好", timeout.Token);
            Assert.Fail("Expected gateway access failure.");
        }
        catch (CaptionServiceException error)
        {
            Assert.That(error.Failure, Is.EqualTo(CaptionFailure.Access));
            Assert.That(error.Message, Does.Not.Contain("sensitive-provider-body"));
        }
        await serve;
    }
}

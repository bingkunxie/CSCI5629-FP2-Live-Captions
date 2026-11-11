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
    public async Task ReverseDirectionSendsEnglishAudioAndReturnsMandarin()
    {
        using var listener = Listen(out string endpoint);
        string multipart = null, chatJson = null;
        var serve = Task.Run(async () => {
            var first = await listener.GetContextAsync();
            using (var reader = new StreamReader(first.Request.InputStream)) multipart = await reader.ReadToEndAsync();
            await Respond(first, "{\"text\":\"The meeting starts at four.\"}");
            var second = await listener.GetContextAsync();
            using (var reader = new StreamReader(second.Request.InputStream)) chatJson = await reader.ReadToEndAsync();
            await Respond(second, "{\"choices\":[{\"message\":{\"content\":\"会议四点开始。\"}}]}");
        });
        var type = Type.GetType("MandarinCaptionGateway, Assembly-CSharp");
        var gateway = (ICaptionGateway)Activator.CreateInstance(type, endpoint, "local-test-key", TranslationDirection.EnglishToMandarin);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string transcript = await gateway.TranscribeAsync(new byte[] { 82, 73, 70, 70 }, timeout.Token);
        string result = await gateway.TranslateAsync(transcript, timeout.Token);
        await serve;
        string language = Regex.Match(multipart, "name=\"language\"[^\\r\\n]*\\r\\n(?:[^\\r\\n]+\\r\\n)*\\r\\n([^\\r\\n]*)").Groups[1].Value;
        Assert.That(language, Is.EqualTo("en"));
        var request = JsonUtility.FromJson<ChatRequest>(chatJson);
        Assert.That(request.messages[0].content, Does.Contain("Mandarin Chinese"));
        Assert.That(request.messages[1].content, Is.EqualTo("The meeting starts at four."));
        Assert.That(result, Is.EqualTo("会议四点开始。"));
    }

    [Serializable] private class SpeechRequest { public string model; public string input; public string voice; public string response_format; }

    [Test]
    public async Task MandarinSpeechUsesMandarinVoiceAndExactCaption()
    {
        using var listener = Listen(out string endpoint);
        string body = null;
        var serve = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            using (var reader = new StreamReader(context.Request.InputStream)) body = await reader.ReadToEndAsync();
            byte[] wav = new byte[48]; Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            context.Response.StatusCode = 200; context.Response.ContentType = "audio/wav"; context.Response.ContentLength64 = wav.Length;
            await context.Response.OutputStream.WriteAsync(wav, 0, wav.Length); context.Response.Close();
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        byte[] result = await ((IVoiceSynthesis)Gateway(endpoint)).SynthesizeAsync("会议四点开始。", "zh", timeout.Token);
        Assert.That(result.Length, Is.GreaterThan(44));
        await serve;
        var request = JsonUtility.FromJson<SpeechRequest>(body);
        Assert.That(request.input, Is.EqualTo("会议四点开始。"));
        Assert.That(request.voice, Is.EqualTo("zh-CN-XiaoxiaoNeural"));
        Assert.That(request.response_format, Is.EqualTo("wav"));
    }

    [Test]
    public async Task VisionClassificationRequiresABooleanAndSendsAnImage()
    {
        using var listener = Listen(out string endpoint);
        string body = null;
        var serve = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            using (var reader = new StreamReader(context.Request.InputStream)) body = await reader.ReadToEndAsync();
            await Respond(context, "{\"choices\":[{\"message\":{\"content\":\"{\\\"thumbs_up\\\":true}\"}}]}");
        });
        var instance = Gateway(endpoint);
        var method = instance.GetType().GetMethod("ClassifyThumbsUpAsync");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        bool result = await (Task<bool>)method.Invoke(instance, new object[] { new byte[] { 255, 216, 255, 217 }, timeout.Token });
        await serve;
        Assert.That(result, Is.True);
        Assert.That(body, Does.Contain("data:image/jpeg;base64,/9j/2Q=="));
        Assert.That(body, Does.Contain("json_object"));
    }

    [Test]
    public async Task VisionDoesNotAcceptStringTrueAsAGesture()
    {
        using var listener = Listen(out string endpoint);
        var serve = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            await Respond(context, "{\"choices\":[{\"message\":{\"content\":\"{\\\"thumbs_up\\\":\\\"true\\\"}\"}}]}");
        });
        var instance = Gateway(endpoint);
        var method = instance.GetType().GetMethod("ClassifyThumbsUpAsync");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await (Task<bool>)method.Invoke(instance, new object[] { new byte[] { 255, 216, 255, 217 }, timeout.Token });
            Assert.Fail("Malformed model output must not trigger the effect.");
        }
        catch (CaptionServiceException error) { Assert.That(error.Failure, Is.EqualTo(CaptionFailure.InvalidResponse)); }
        await serve;
    }


    [TestCase("en", 1, "en-GB-SoniaNeural")]
    [TestCase("en", 2, "en-AU-NatashaNeural")]
    [TestCase("zh", 1, "zh-TW-HsiaoChenNeural")]
    public async Task SelectedAccentReachesTheActualSpeechRequest(string language, int preset, string expected)
    {
        using var listener = Listen(out string endpoint);
        string body = null;
        var serve = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            using (var reader = new StreamReader(context.Request.InputStream)) body = await reader.ReadToEndAsync();
            byte[] wav = new byte[48]; Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            context.Response.StatusCode = 200; context.Response.ContentLength64 = wav.Length;
            await context.Response.OutputStream.WriteAsync(wav,0,wav.Length); context.Response.Close();
        });
        var instance = Gateway(endpoint);
        instance.GetType().GetProperty("VoicePreset").SetValue(instance,preset);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await ((IVoiceSynthesis)instance).SynthesizeAsync("Exact accepted words",language,timeout.Token);
        await serve;
        var request = JsonUtility.FromJson<SpeechRequest>(body);
        Assert.That(request.voice, Is.EqualTo(expected));
        Assert.That(request.input, Is.EqualTo("Exact accepted words"));
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

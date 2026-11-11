using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FP2.Captions;
using NUnit.Framework;

public class TranslatedVoiceTests
{
    private sealed class Service : IVoiceSynthesis
    {
        public int Calls;
        public string Text, Language;
        public Func<Task<byte[]>> Result = () => Task.FromResult(new byte[] { 1 });
        public Task<byte[]> SynthesizeAsync(string text, string language, CancellationToken token)
        { Calls++; Text = text; Language = language; return Result(); }
    }
    private sealed class Output : IVoicePlayback
    {
        public readonly List<byte[]> Played = new List<byte[]>();
        public bool Stopped;
        public void Play(byte[] wav) { Played.Add(wav); Stopped = false; }
        public void Stop() => Stopped = true;
    }
    [Test]
    public async Task VoiceOffDoesNotRequestOrPlaySpeech()
    {
        var service = new Service(); var output = new Output();
        using var voice = new TranslatedVoice(service, output);
        await voice.PresentAsync("The meeting starts at four.", "en");
        Assert.That(service.Calls, Is.Zero); Assert.That(output.Played, Is.Empty);
    }
    [Test]
    public async Task EnabledVoiceSpeaksExactCaptionInTargetLanguage()
    {
        var service = new Service(); var output = new Output();
        using var voice = new TranslatedVoice(service, output);
        voice.SetEnabled(true);
        await voice.PresentAsync("会议四点开始。", "zh");
        Assert.That(service.Text, Is.EqualTo("会议四点开始。"));
        Assert.That(service.Language, Is.EqualTo("zh"));
        Assert.That(output.Played.Count, Is.EqualTo(1));
        Assert.That(voice.State, Is.EqualTo(VoiceState.Speaking));
    }
    [Test]
    public async Task MutingDiscardsLateAudioEvenIfProviderIgnoresCancellation()
    {
        var pending = new TaskCompletionSource<byte[]>();
        var service = new Service { Result = () => pending.Task }; var output = new Output();
        using var voice = new TranslatedVoice(service, output); voice.SetEnabled(true);
        var task = voice.PresentAsync("Hello", "en"); voice.SetEnabled(false);
        pending.SetResult(new byte[] { 1 }); await task;
        Assert.That(output.Played, Is.Empty); Assert.That(output.Stopped, Is.True);
        Assert.That(voice.State, Is.EqualTo(VoiceState.Off));
    }
    [Test]
    public async Task NewCaptionCannotBeReplacedByOlderAudio()
    {
        var pending = new TaskCompletionSource<byte[]>();
        var service = new Service { Result = () => pending.Task }; var output = new Output();
        using var voice = new TranslatedVoice(service, output); voice.SetEnabled(true);
        var old = voice.PresentAsync("Old", "en");
        service.Result = () => Task.FromResult(new byte[] { 2 });
        await voice.PresentAsync("新的", "zh"); pending.SetResult(new byte[] { 1 }); await old;
        Assert.That(output.Played.Count, Is.EqualTo(1)); Assert.That(output.Played[0], Is.EqualTo(new byte[] { 2 }));
    }
    [Test]
    public async Task CancelStopsPlaybackAndAllowsNextCaption()
    {
        var service = new Service(); var output = new Output();
        using var voice = new TranslatedVoice(service, output); voice.SetEnabled(true);
        await voice.PresentAsync("First", "en"); voice.Cancel();
        Assert.That(output.Stopped, Is.True); Assert.That(voice.State, Is.EqualTo(VoiceState.Ready));
        await voice.PresentAsync("Second", "en"); Assert.That(output.Played.Count, Is.EqualTo(2));
    }
    [Test]
    public async Task SynthesisFailureIsRecoverableWithoutPlayingAnything()
    {
        var service = new Service { Result = () => throw new Exception("private-provider-error") }; var output = new Output();
        using var voice = new TranslatedVoice(service, output); voice.SetEnabled(true);
        await voice.PresentAsync("Hello", "en");
        Assert.That(voice.State, Is.EqualTo(VoiceState.Error)); Assert.That(output.Played, Is.Empty);
        service.Result = () => Task.FromResult(new byte[] { 3 });
        await voice.PresentAsync("Try again", "en"); Assert.That(output.Played.Count, Is.EqualTo(1));
    }
}

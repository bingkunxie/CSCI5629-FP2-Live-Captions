using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FP2.Captions;
using NUnit.Framework;

public class CaptionImprovementTests
{
    private sealed class Gateway : ICaptionGateway
    {
        public Func<Task<string>> Recognition = () => Task.FromResult("会议四点开始");
        public Task<string> TranscribeAsync(byte[] audio, CancellationToken token) => Recognition();
        public Task<string> TranslateAsync(string text, CancellationToken token) => Task.FromResult("The meeting starts at four.");
    }

    [Test]
    public async Task ReviewDoesNotPublishOrSaveUntilUserAccepts()
    {
        using var session = new CaptionSession(new Gateway());
        session.BeginCapture(); await session.SubmitAsync(new byte[] { 1 }, true);
        Assert.That(session.State, Is.EqualTo(CaptionState.Reviewing));
        Assert.That(session.CaptionRevision, Is.Zero);
        Assert.That(session.Transcript, Is.Empty);
        Assert.That(session.DraftTranscript, Is.EqualTo("会议四点开始"));
        Assert.That(session.AcceptDraft(), Is.True);
        Assert.That(session.WasReviewed, Is.True);
        Assert.That(session.Transcript, Is.EqualTo("会议四点开始"));
        Assert.That(session.TurnId, Is.Not.Empty);
        Assert.That(session.AcceptDraft(), Is.False);
        Assert.That(session.CaptionRevision, Is.EqualTo(1));
    }

    [Test]
    public async Task DiscardedDraftKeepsPreviousApprovedTurn()
    {
        using var session = new CaptionSession(new Gateway());
        session.BeginCapture(); await session.SubmitAsync(new byte[] { 1 });
        string id = session.TurnId;
        session.BeginCapture(); await session.SubmitAsync(new byte[] { 2 }, true);
        session.Cancel();
        Assert.That(session.AcceptDraft(), Is.False);
        Assert.That(session.TurnId, Is.EqualTo(id));
        Assert.That(session.DraftCaption, Is.Empty);
        Assert.That(session.CaptionRevision, Is.EqualTo(1));
    }

    [Test]
    public async Task CancelledRecognitionCannotCreateADraftAfterRetry()
    {
        var pending = new TaskCompletionSource<string>();
        var gateway = new Gateway { Recognition = () => pending.Task };
        using var session = new CaptionSession(gateway);
        session.BeginCapture(); Task old = session.SubmitAsync(new byte[] { 1 }, true);
        session.Cancel();
        gateway.Recognition = () => Task.FromResult("新内容");
        session.BeginCapture(); await session.SubmitAsync(new byte[] { 2 }, true);
        pending.SetResult("过期内容"); await old;
        Assert.That(session.DraftTranscript, Is.EqualTo("新内容"));
    }

    [Test]
    public void SilenceAndClippingAreRejectedBeforeAnyServiceRequest()
    {
        Assert.That(AudioConditioner.Process(new float[16000], 1, 16000, true).Reject, Is.True);
        var clipped = Enumerable.Range(0,16000).Select(i => i % 2 == 0 ? 1f : -1f).ToArray();
        Assert.That(AudioConditioner.Process(clipped, 1, 16000, true).Reject, Is.True);
    }

    [Test]
    public void SteadyInterferenceRequiresReview()
    {
        var tone = Enumerable.Range(0,32000).Select(i => (float)(.1*Math.Sin(i*2*Math.PI*600/16000))).ToArray();
        var result = AudioConditioner.Process(tone,1,16000,true);
        Assert.That(result.Reject, Is.False);
        Assert.That(result.NeedsReview, Is.True);
        Assert.That(result.EnvelopeContrastDb, Is.LessThan(8));
    }

    [Test]
    public void HighPassAttenuatesRumbleWhileRetainingSpeechBandEnergy()
    {
        float[] Signal(double frequency) => Enumerable.Range(0,32000)
            .Select(i => (float)(.1*Math.Sin(i*2*Math.PI*frequency/16000))).ToArray();
        double Energy(float[] x) => Math.Sqrt(x.Skip(1600).Select(v => (double)v*v).Average());
        var rumble = Signal(30); var speech = Signal(1000);
        double r = Energy(AudioConditioner.Process(rumble,1,16000,true).Samples)/Energy(rumble);
        double v = Energy(AudioConditioner.Process(speech,1,16000,true).Samples)/Energy(speech);
        Assert.That(r, Is.LessThan(.4));
        Assert.That(v, Is.GreaterThan(.9));
    }

    [Test]
    public void StereoIsDownmixedAndInvalidSamplesAreRejected()
    {
        var stereo = Enumerable.Range(0,32000).Select(i => (float)(.03*Math.Sin((i/2)*.3))).ToArray();
        Assert.That(AudioConditioner.Process(stereo,2,16000,true).Samples.Length, Is.EqualTo(16000));
        stereo[0] = float.NaN;
        Assert.Throws<ArgumentException>(() => AudioConditioner.Process(stereo,2,16000,true));
    }

    private static ConversationEntry Entry(string id) => new ConversationEntry {
        Id=id, UtcTime="2026-11-11T12:00:00Z", Speaker="A", Direction="MandarinToEnglish",
        Source="你好\n四点", Translation="Hello\nFour o'clock", Reviewed=true, ProcessingSeconds=2.5
    };

    [Test]
    public void NotebookPersistsDeduplicatesAndExportsUnicodeTurns()
    {
        string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory ?? Environment.CurrentDirectory, "notebook-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"notes.dat");
            var book=new ConversationNotebook(file);
            Assert.That(book.Add(Entry("a")), Is.True);
            Assert.That(book.Add(Entry("a")), Is.False);
            var loaded=new ConversationNotebook(file);
            Assert.That(loaded.Entries.Count, Is.EqualTo(1));
            Assert.That(loaded.Entries[0].Source, Is.EqualTo("你好\n四点"));
            Assert.That(loaded.ToMarkdown(), Does.Contain("> 你好\n> 四点"));
            Assert.That(loaded.ToMarkdown(), Does.Contain("User confirmed"));
        }
        finally { Directory.Delete(folder,true); }
    }

    [Test]
    public void NotebookRecoversLastSnapshotAndClearRemovesBackup()
    {
        string folder=Path.Combine(TestContext.CurrentContext.WorkDirectory ?? Environment.CurrentDirectory,"notebook-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"notes.dat");
            var book=new ConversationNotebook(file);
            book.Add(Entry("a")); book.Add(Entry("b"));
            File.WriteAllText(file,"broken");
            var recovered=new ConversationNotebook(file);
            Assert.That(recovered.RecoveredFromBackup, Is.True);
            Assert.That(recovered.Entries.Count, Is.EqualTo(1));
            recovered.Clear();
            Assert.That(new ConversationNotebook(file).Entries.Count, Is.Zero);
            Assert.That(File.Exists(file+".bak"), Is.False);
        }
        finally { Directory.Delete(folder,true); }
    }

    [Test]
    public void NotebookWriteFailureKeepsInMemoryTurns()
    {
        string folder=Path.Combine(TestContext.CurrentContext.WorkDirectory ?? Environment.CurrentDirectory,"notebook-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"notes.dat");
            var book=new ConversationNotebook(file); book.Add(Entry("a"));
            Directory.CreateDirectory(file+".tmp");
            Assert.Throws<UnauthorizedAccessException>(() => book.Add(Entry("b")));
            Assert.That(book.Entries.Count, Is.EqualTo(1));
            Assert.That(new ConversationNotebook(file).Entries.Count, Is.EqualTo(1));
        }
        finally { Directory.Delete(folder,true); }
    }

    [Test]
    public void VoiceAccentsResolveForBothLanguages()
    {
        Assert.That(VoicePresets.Id("en",0), Is.EqualTo("alloy"));
        Assert.That(VoicePresets.Id("en",1), Is.EqualTo("en-GB-SoniaNeural"));
        Assert.That(VoicePresets.Id("en",2), Is.EqualTo("en-AU-NatashaNeural"));
        Assert.That(VoicePresets.Id("zh",0), Is.EqualTo("zh-CN-XiaoxiaoNeural"));
        Assert.That(VoicePresets.Id("zh",1), Is.EqualTo("zh-TW-HsiaoChenNeural"));
        Assert.That(VoicePresets.Id("en",3), Is.EqualTo(VoicePresets.Id("en",0)));
    }
}

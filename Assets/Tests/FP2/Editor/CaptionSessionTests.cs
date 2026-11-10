using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FP2.Captions;

public class CaptionSessionTests
{
    private sealed class Gateway : ICaptionGateway
    {
        public Func<byte[], CancellationToken, Task<string>> Transcribe = (audio, token) => Task.FromResult("会议不在三点，是四点。");
        public Func<string, CancellationToken, Task<string>> Translate = (text, token) => Task.FromResult("The meeting is at four, not three.");
        public Task<string> TranscribeAsync(byte[] audio, CancellationToken token) => Transcribe(audio, token);
        public Task<string> TranslateAsync(string text, CancellationToken token) => Translate(text, token);
    }

    [Test]
    public async Task SuccessfulTurnPublishesTranslationAfterTranscribingAndTranslating()
    {
        var gateway = new Gateway();
        string received = null;
        gateway.Translate = (text, token) => { received = text; return Task.FromResult("  The meeting is at four, not three.  "); };
        using var session = new CaptionSession(gateway);
        var states = new List<CaptionState>();
        session.Changed += () => states.Add(session.State);
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 1 });
        Assert.That(received, Is.EqualTo("会议不在三点，是四点。"));
        Assert.That(session.Caption, Is.EqualTo("The meeting is at four, not three."));
        Assert.That(states, Is.EqualTo(new[] { CaptionState.Listening, CaptionState.Transcribing, CaptionState.Translating, CaptionState.Ready }));
    }

    [Test]
    public async Task EmptyTranscriptionDoesNotAskForATranslationAndAllowsRetry()
    {
        var gateway = new Gateway { Transcribe = (audio, token) => Task.FromResult("  ") };
        gateway.Translate = (text, token) => throw new AssertionException("Empty speech must not reach translation.");
        using var session = new CaptionSession(gateway);
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 1 });
        Assert.That(session.State, Is.EqualTo(CaptionState.Error));
        Assert.That(session.Caption, Is.Empty);
        Assert.That(session.BeginCapture(), Is.True);
    }

    [Test]
    public async Task FailedRequestKeepsPreviousCaptionAndHidesProviderDetails()
    {
        var gateway = new Gateway();
        using var session = new CaptionSession(gateway);
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 1 });
        gateway.Transcribe = (audio, token) => throw new Exception("sensitive-provider-response");
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 2 });
        Assert.That(session.State, Is.EqualTo(CaptionState.Error));
        Assert.That(session.Caption, Is.EqualTo("The meeting is at four, not three."));
        Assert.That(session.Status, Does.Not.Contain("sensitive-provider-response"));
        Assert.That(session.BeginCapture(), Is.True);
    }

    [Test]
    public async Task CancelledOldTurnCannotOverwriteNewTranslation()
    {
        var oldTranscript = new TaskCompletionSource<string>();
        var gateway = new Gateway { Transcribe = (audio, token) => oldTranscript.Task };
        using var session = new CaptionSession(gateway);
        Assert.That(session.BeginCapture(), Is.True);
        Task oldTurn = session.SubmitAsync(new byte[] { 1 });
        Assert.That(session.State, Is.EqualTo(CaptionState.Transcribing));
        session.Cancel();
        gateway.Transcribe = (audio, token) => Task.FromResult("今天没有会议。");
        gateway.Translate = (text, token) => Task.FromResult("There is no meeting today.");
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 2 });
        oldTranscript.SetResult("旧的会议消息");
        await oldTurn;
        Assert.That(session.Caption, Is.EqualTo("There is no meeting today."));
        Assert.That(session.State, Is.EqualTo(CaptionState.Ready));
    }

    [Test]
    public async Task ProcessingRejectsAnotherCaptureUntilCancelled()
    {
        var pending = new TaskCompletionSource<string>();
        using var session = new CaptionSession(new Gateway { Transcribe = (audio, token) => pending.Task });
        Assert.That(session.BeginCapture(), Is.True);
        Assert.That(session.BeginCapture(), Is.False);
        Task turn = session.SubmitAsync(new byte[] { 1 });
        Assert.That(session.BeginCapture(), Is.False);
        session.Cancel();
        pending.SetResult("旧的消息");
        await turn;
        Assert.That(session.BeginCapture(), Is.True);
    }

    [Test]
    public async Task CancelDuringTranslationPreventsPublishingEvenIfServiceIgnoresCancellation()
    {
        var pending = new TaskCompletionSource<string>();
        using var session = new CaptionSession(new Gateway { Translate = (text, token) => pending.Task });
        Assert.That(session.BeginCapture(), Is.True);
        Task turn = session.SubmitAsync(new byte[] { 1 });
        Assert.That(session.State, Is.EqualTo(CaptionState.Translating));
        session.Cancel();
        pending.SetResult("An obsolete result.");
        await turn;
        Assert.That(session.Caption, Is.Empty);
        Assert.That(session.State, Is.EqualTo(CaptionState.Ready));
    }

    [Test]
    public async Task CancellingANewTurnKeepsRetainedCaptionMarkedAsPrevious()
    {
        using var session = new CaptionSession(new Gateway());
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 1 });
        Assert.That(session.CaptionIsPrevious, Is.False);
        Assert.That(session.BeginCapture(), Is.True);
        session.Cancel();
        Assert.That(session.State, Is.EqualTo(CaptionState.Ready));
        Assert.That(session.Caption, Is.EqualTo("The meeting is at four, not three."));
        Assert.That(session.CaptionIsPrevious, Is.True);
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 2 });
        Assert.That(session.CaptionIsPrevious, Is.False);
    }

    [Test]
    public async Task RepeatedIdenticalTranslationStillPublishesANewResult()
    {
        using var session = new CaptionSession(new Gateway());
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 1 });
        int first = session.CaptionRevision;
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 2 });
        Assert.That(session.CaptionRevision, Is.GreaterThan(first));
    }

    [Test]
    public async Task EmptyTranslationIsAnErrorInsteadOfAReadyBlankCaption()
    {
        using var session = new CaptionSession(new Gateway { Translate = (text, token) => Task.FromResult("\n") });
        Assert.That(session.BeginCapture(), Is.True);
        await session.SubmitAsync(new byte[] { 1 });
        Assert.That(session.State, Is.EqualTo(CaptionState.Error));
        Assert.That(session.Caption, Is.Empty);
    }
}

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FP2.Captions
{
    public enum CaptionState { Ready, Listening, Transcribing, Translating, Error }
    public interface ICaptionGateway
    {
        Task<string> TranscribeAsync(byte[] audio, CancellationToken token);
        Task<string> TranslateAsync(string text, CancellationToken token);
    }

    public enum CaptionFailure { Network, Access, RateLimit, InvalidResponse }

    public sealed class CaptionServiceException : Exception
    {
        public CaptionFailure Failure { get; }
        public CaptionServiceException(CaptionFailure failure) : base("Caption service failed") => Failure = failure;
    }

    public sealed class CaptionSession : IDisposable
    {
        private readonly ICaptionGateway gateway;
        private CancellationTokenSource active;
        private int generation;
        private bool disposed;

        public CaptionSession(ICaptionGateway gateway) => this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        public CaptionState State { get; private set; }
        public string Caption { get; private set; } = "";
        public int CaptionRevision { get; private set; }
        public bool CaptionIsPrevious { get; private set; }
        public string Status { get; private set; } = "Ready";
        public double ProcessingSeconds { get; private set; }
        public double TranscriptionSeconds { get; private set; }
        public double TranslationSeconds { get; private set; }
        public event Action Changed;

        public bool BeginCapture()
        {
            if (disposed || State == CaptionState.Listening || State == CaptionState.Transcribing || State == CaptionState.Translating) return false;
            generation++;
            CaptionIsPrevious = Caption.Length > 0;
            SetState(CaptionState.Listening, "Listening to Mandarin…");
            return true;
        }

        public async Task SubmitAsync(byte[] audio)
        {
            if (disposed || State != CaptionState.Listening) return;
            if (audio == null || audio.Length == 0) { RejectCapture("No audio. Hold the trigger while someone speaks."); return; }
            int turn = generation;
            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            active = operation;
            var clock = Stopwatch.StartNew();
            TranscriptionSeconds = TranslationSeconds = ProcessingSeconds = 0;
            try
            {
                SetState(CaptionState.Transcribing, "Recognizing Mandarin…");
                string transcript = (await gateway.TranscribeAsync(audio, operation.Token))?.Trim();
                operation.Token.ThrowIfCancellationRequested();
                if (!IsCurrent(turn)) return;
                TranscriptionSeconds = clock.Elapsed.TotalSeconds;
                if (string.IsNullOrWhiteSpace(transcript)) { SetState(CaptionState.Error, "No speech detected. Please try again."); return; }
                SetState(CaptionState.Translating, "Translating to English…");
                string translation = (await gateway.TranslateAsync(transcript, operation.Token))?.Trim();
                operation.Token.ThrowIfCancellationRequested();
                if (!IsCurrent(turn)) return;
                if (string.IsNullOrWhiteSpace(translation)) { SetState(CaptionState.Error, "No translation returned. Please try again."); return; }
                ProcessingSeconds = clock.Elapsed.TotalSeconds;
                TranslationSeconds = ProcessingSeconds - TranscriptionSeconds;
                Caption = translation;
                CaptionRevision++;
                CaptionIsPrevious = false;
                SetState(CaptionState.Ready, "Translation ready");
            }
            catch (OperationCanceledException)
            {
                if (IsCurrent(turn)) SetState(CaptionState.Error, "Request timed out. Check your connection and try again.");
            }
            catch (CaptionServiceException error)
            {
                if (IsCurrent(turn)) SetState(CaptionState.Error, FailureMessage(error.Failure));
            }
            catch (Exception)
            {
                // Never publish provider messages: they can contain recordings or credentials.
                if (IsCurrent(turn)) SetState(CaptionState.Error, "Request failed. Check gateway access and try again.");
            }
            finally
            {
                if (active == operation) active = null;
                operation.Dispose();
            }
        }

        public void RejectCapture(string reason)
        {
            if (disposed) return;
            generation++;
            active?.Cancel();
            active = null;
            SetState(CaptionState.Error, reason);
        }

        public void Cancel()
        {
            generation++;
            active?.Cancel();
            active = null;
            SetState(CaptionState.Ready, "Ready");
        }

        private bool IsCurrent(int turn) => !disposed && turn == generation;
        private void SetState(CaptionState state, string status)
        {
            State = state; Status = status;
            Changed?.Invoke();
        }

        private static string FailureMessage(CaptionFailure failure)
        {
            switch (failure)
            {
                case CaptionFailure.Network: return "Cannot reach the gateway. Check campus Wi-Fi or the USB/VPN route.";
                case CaptionFailure.Access: return "Gateway access denied. Check the course key and model access.";
                case CaptionFailure.RateLimit: return "Gateway limit reached. Wait and check the course budget.";
                default: return "Gateway returned an invalid result. Please try again.";
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Cancel();
            Changed = null;
        }
    }
}

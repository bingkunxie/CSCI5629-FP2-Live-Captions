using System;
using System.Threading;
using System.Threading.Tasks;

namespace FP2.Captions
{
    public interface IVoiceSynthesis
    {
        Task<byte[]> SynthesizeAsync(string text, string language, CancellationToken token);
    }
    public interface IVoicePlayback
    {
        void Play(byte[] wav);
        void Stop();
    }
    public enum VoiceState { Off, Ready, Preparing, Speaking, Error }

    public sealed class TranslatedVoice : IDisposable
    {
        private readonly IVoiceSynthesis synthesis;
        private readonly IVoicePlayback playback;
        private CancellationTokenSource active;
        private int generation;
        private bool disposed;
        public TranslatedVoice(IVoiceSynthesis synthesis, IVoicePlayback playback)
        { this.synthesis = synthesis; this.playback = playback; }
        public bool Enabled { get; private set; }
        public VoiceState State { get; private set; }
        public event Action Changed;
        public void SetEnabled(bool enabled)
        {
            if (disposed) return;
            Enabled = enabled; Cancel();
        }
        public async Task PresentAsync(string text, string language)
        {
            if (disposed || !Enabled || string.IsNullOrWhiteSpace(text)) return;
            Cancel();
            int request = generation;
            var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            active = operation;
            SetState(VoiceState.Preparing);
            try
            {
                byte[] wav = await synthesis.SynthesizeAsync(text, language, operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (disposed || !Enabled || request != generation) return;
                if (wav == null || wav.Length == 0) throw new InvalidOperationException();
                playback.Play(wav); SetState(VoiceState.Speaking);
            }
            catch (Exception)
            {
                if (!disposed && Enabled && request == generation) SetState(VoiceState.Error);
            }
            finally
            {
                if (active == operation) active = null;
                operation.Dispose();
            }
        }
        public void Cancel()
        {
            generation++; active?.Cancel(); active = null;
            playback.Stop(); SetState(Enabled ? VoiceState.Ready : VoiceState.Off);
        }
        public void PlaybackEnded() { if (State == VoiceState.Speaking) SetState(Enabled ? VoiceState.Ready : VoiceState.Off); }
        private void SetState(VoiceState state) { State = state; Changed?.Invoke(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Enabled = false; Cancel(); Changed = null;
        }
    }
}

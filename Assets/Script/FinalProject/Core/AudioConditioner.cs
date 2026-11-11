using System;

namespace FP2.Captions
{
    public sealed class ConditionedAudio
    {
        public float[] Samples;
        public double Rms, NoiseRms, EnvelopeContrastDb, ClippedFraction;
        public bool Reject, NeedsReview;
        public string Message;
    }

    // Conservative conditioning for one microphone. This does not identify or separate speakers.
    public static class AudioConditioner
    {
        public static ConditionedAudio Process(float[] input, int channels, int sampleRate, bool reduceNoise)
        {
            if (input == null || channels < 1 || sampleRate < 8000 || input.Length % channels != 0)
                throw new ArgumentException("Invalid microphone format.");
            int count = input.Length / channels;
            if (count < sampleRate / 4)
                return new ConditionedAudio { Samples = Array.Empty<float>(), Reject = true, Message = "Too short. Hold the trigger while one person speaks." };
            var samples = new float[count];
            double energy = 0;
            int clipped = 0;
            double lastInput = 0, lastOutput = 0;
            double alpha = Math.Exp(-2 * Math.PI * 80 / sampleRate);
            for (int i = 0; i < count; i++)
            {
                double value = 0;
                for (int c = 0; c < channels; c++)
                {
                    float sample = input[i * channels + c];
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new ArgumentException("Invalid microphone sample.");
                    value += sample / channels;
                }
                if (Math.Abs(value) >= .985) clipped++;
                double highPassed = alpha * (lastOutput + value - lastInput);
                lastInput = value; lastOutput = highPassed;
                samples[i] = (float)(reduceNoise ? highPassed : value);
                energy += samples[i] * samples[i];
            }
            int window = sampleRate / 50; // 20 ms envelopes retain consonant transitions.
            int windows = (count + window - 1) / window;
            var levels = new double[windows];
            for (int f = 0; f < windows; f++)
            {
                int end = Math.Min(count, (f + 1) * window);
                double sum = 0;
                for (int i = f * window; i < end; i++) sum += samples[i] * samples[i];
                levels[f] = Math.Sqrt(sum / (end - f * window));
            }
            var sorted = (double[])levels.Clone();
            Array.Sort(sorted);
            double noise = sorted[(int)((windows - 1) * .15)];
            double peak = sorted[(int)((windows - 1) * .90)];
            double rms = Math.Sqrt(energy / count);
            double contrast = 20 * Math.Log10((peak + 1e-7) / (noise + 1e-7));
            var result = new ConditionedAudio {
                Samples = samples, Rms = rms, NoiseRms = noise, EnvelopeContrastDb = contrast,
                ClippedFraction = (double)clipped / count
            };
            if (rms < .001 || peak < .0015)
            {
                result.Reject = true; result.Message = "Audio is too quiet. Move closer and repeat one speaker at a time.";
                return result;
            }
            if (result.ClippedFraction > .03)
            {
                result.Reject = true; result.Message = "Audio is distorted. Move farther from the microphone and repeat.";
                return result;
            }
            result.NeedsReview = contrast < 8 || result.ClippedFraction > .003;
            result.Message = result.NeedsReview
                ? "Background sound or low speech contrast. Check the words before accepting."
                : "Audio captured. Keep one speaker per turn.";
            if (!reduceNoise) return result;
            // Attenuate low-energy gaps without hard muting or amplifying the background.
            double gain = 1;
            double floor = Math.Min(noise, peak / 4); // Never treat sustained speech as all noise.
            for (int f = 0; f < windows; f++)
            {
                double ratio = levels[f] / Math.Max(.0003, floor * 2);
                double target = Math.Max(.22, Math.Min(1, ratio * ratio));
                int end = Math.Min(count, (f + 1) * window);
                for (int i = f * window; i < end; i++)
                {
                    gain += (target - gain) * (target > gain ? .025 : .003);
                    samples[i] *= (float)gain;
                }
            }
            return result;
        }
    }
}

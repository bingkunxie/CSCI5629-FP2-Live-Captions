namespace FP2.Captions
{
    public enum ThumbsUpSource { LeftHand, RightHand, Camera }

    public sealed class ThumbsUpGate
    {
        private sealed class Pose
        {
            public double? PositiveSince;
            public double? NegativeSince;
            public bool Held;
        }

        private readonly Pose[] poses = { new Pose(), new Pose(), new Pose() };
        private double lastPop = double.NegativeInfinity;
        private const double HoldSeconds = .25;
        private const double ReleaseSeconds = .2;
        private const double CooldownSeconds = 4;

        /// <summary>
        /// Unknown tracking never counts as a release. Sparse camera samples use one positive/negative
        /// observation; native tracking must stabilize. sampleTime deduplicates delayed camera results
        /// against native gestures observed while that frame was being classified.
        /// </summary>
        public bool Observe(ThumbsUpSource source, bool? detected, double time, double? sampleTime = null)
        {
            Pose pose = poses[(int)source];
            if (!detected.HasValue)
            {
                pose.PositiveSince = pose.NegativeSince = null;
                return false;
            }
            if (!detected.Value)
            {
                pose.PositiveSince = null;
                if (!pose.NegativeSince.HasValue) pose.NegativeSince = time;
                if (source == ThumbsUpSource.Camera || time - pose.NegativeSince.Value >= ReleaseSeconds)
                    pose.Held = false;
                return false;
            }

            pose.NegativeSince = null;
            if (pose.Held) return false;
            if (!pose.PositiveSince.HasValue) pose.PositiveSince = time;
            if (source != ThumbsUpSource.Camera && time - pose.PositiveSince.Value < HoldSeconds) return false;
            // Consume duplicates too; a continuously held pose must not pop later when cooldown ends.
            pose.Held = true;
            if ((sampleTime ?? time) - lastPop < CooldownSeconds) return false;
            if (source == ThumbsUpSource.Camera &&
                (IsCurrentlyHeld(ThumbsUpSource.LeftHand) || IsCurrentlyHeld(ThumbsUpSource.RightHand))) return false;
            lastPop = time;
            return true;
        }

        private bool IsCurrentlyHeld(ThumbsUpSource source) => poses[(int)source].Held && poses[(int)source].PositiveSince.HasValue;
        public void Reset(ThumbsUpSource source) => poses[(int)source] = new Pose();
    }
}

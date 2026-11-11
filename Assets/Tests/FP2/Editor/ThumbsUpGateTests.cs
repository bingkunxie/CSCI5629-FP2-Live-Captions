using FP2.Captions;
using NUnit.Framework;

public class ThumbsUpGateTests
{
    [TestCase(8d)]
    [TestCase(12d)]
    public void FrameCapturedDuringConsumedNativePoseDoesNotPopAfterRelease(double responseTime)
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, .3), Is.True);
        gate.Observe(ThumbsUpSource.LeftHand, true, 6);
        bool snapshot = gate.NativeGestureIsHeld;
        gate.Observe(ThumbsUpSource.LeftHand, false, 7);
        gate.Observe(ThumbsUpSource.LeftHand, false, 7.3);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, responseTime, 6, snapshot), Is.False);
    }

    [Test]
    public void StaleCameraFrameDoesNotPopOrConsumeAFreshGesture()
    {
        var gate = new ThumbsUpGate();
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 10, 0), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 11, 10), Is.True);
    }

    [Test]
    public void NativePoseMustRemainStableAndOnlyPopsOnceWhileHeld()
    {
        var gate = new ThumbsUpGate();
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 0), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, .20), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, .30), Is.True);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 10), Is.False);
    }

    [Test]
    public void BriefPoseDoesNotAccumulateAcrossRelease()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        gate.Observe(ThumbsUpSource.LeftHand, false, .1);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 1), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 1.3), Is.True);
    }

    [Test]
    public void StableReleaseRearmsButBriefTrackingFlickerDoesNot()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        gate.Observe(ThumbsUpSource.LeftHand, true, .3);
        gate.Observe(ThumbsUpSource.LeftHand, false, 5);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 5.1), Is.False);
        gate.Observe(ThumbsUpSource.LeftHand, false, 6);
        gate.Observe(ThumbsUpSource.LeftHand, false, 6.3);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 7), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 7.3), Is.True);
    }

    [Test]
    public void TrackingLossCannotRearmAHeldGesture()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.RightHand, true, 0);
        gate.Observe(ThumbsUpSource.RightHand, true, .3);
        gate.Observe(ThumbsUpSource.RightHand, null, 1);
        gate.Observe(ThumbsUpSource.RightHand, null, 5);
        Assert.That(gate.Observe(ThumbsUpSource.RightHand, true, 6), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.RightHand, true, 6.5), Is.False);
    }

    [Test]
    public void CameraSamplesPopOnceUntilANegativeSampleRearms()
    {
        var gate = new ThumbsUpGate();
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 0), Is.True);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 10), Is.False);
        gate.Observe(ThumbsUpSource.Camera, false, 13);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 16), Is.True);
    }

    [Test]
    public void SharedCooldownConsumesDuplicateInsteadOfDeferringItsPop()
    {
        var gate = new ThumbsUpGate();
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 0), Is.True);
        gate.Observe(ThumbsUpSource.LeftHand, true, .1);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, .4), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 10), Is.False);
        gate.Observe(ThumbsUpSource.LeftHand, false, 11);
        gate.Observe(ThumbsUpSource.LeftHand, false, 11.3);
        gate.Observe(ThumbsUpSource.LeftHand, true, 12);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 12.3), Is.True);
    }

    [Test]
    public void CameraDoesNotEchoCurrentlyHeldWearerPoseAfterLongServiceDelay()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, .3), Is.True);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 12), Is.False);
    }

    [Test]
    public void ResettingCameraDoesNotRearmOrForgetWearerCooldown()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        gate.Observe(ThumbsUpSource.LeftHand, true, .3);
        gate.Reset(ThumbsUpSource.Camera);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 10), Is.False);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 11), Is.False);
    }

    [Test]
    public void LostWearerTrackingDoesNotSuppressOtherSpeakerCameraGesturesForever()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        gate.Observe(ThumbsUpSource.LeftHand, true, .3);
        gate.Observe(ThumbsUpSource.LeftHand, null, 1);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 8), Is.True);
        Assert.That(gate.Observe(ThumbsUpSource.LeftHand, true, 12), Is.False);
    }

    [Test]
    public void DelayedCameraSampleCapturedDuringNativePopCannotEchoAfterRelease()
    {
        var gate = new ThumbsUpGate();
        gate.Observe(ThumbsUpSource.LeftHand, true, 0);
        gate.Observe(ThumbsUpSource.LeftHand, true, .3);
        gate.Observe(ThumbsUpSource.LeftHand, false, 1);
        gate.Observe(ThumbsUpSource.LeftHand, false, 1.3);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 12, .4), Is.False);
        gate.Observe(ThumbsUpSource.Camera, false, 13);
        Assert.That(gate.Observe(ThumbsUpSource.Camera, true, 16, 15), Is.True);
    }
}

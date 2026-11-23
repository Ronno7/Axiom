using GeometricFlight.Input;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class CalibrationWizardTests
    {
        private static ControllerPacket Stick(int x, int y) => new ControllerPacket(x, y, 512, true, false);

        [Test]
        public void FullRunWritesCenterAndThrottleRange()
        {
            var calibration = new ControllerCalibration();
            var wizard = new CalibrationWizard(calibration);
            Assert.That(wizard.Capture(Stick(498, 530), 0f, false), Is.True);
            Assert.That(wizard.Capture(Stick(512, 512), 31f, true), Is.True);
            Assert.That(calibration.throttleMin, Is.EqualTo(0), "range unchanged until both ends are captured");
            Assert.That(wizard.Capture(Stick(512, 512), 996.4f, true), Is.True);
            Assert.That(wizard.Current, Is.EqualTo(CalibrationWizard.Step.Check));
            Assert.That(calibration.stickCenterX, Is.EqualTo(498));
            Assert.That(calibration.stickCenterY, Is.EqualTo(530));
            Assert.That(calibration.throttleMin, Is.EqualTo(31));
            Assert.That(calibration.throttleMax, Is.EqualTo(996));
        }

        [Test]
        public void RejectsStickThatIsNotReleased()
        {
            var wizard = new CalibrationWizard(new ControllerCalibration());
            Assert.That(wizard.Capture(Stick(1000, 512), 0f, false), Is.False);
            Assert.That(wizard.Current, Is.EqualTo(CalibrationWizard.Step.Center));
            Assert.That(wizard.Error, Does.Contain("centered"));
        }

        [Test]
        public void RejectsAMissingReadingAndTooShortTravel()
        {
            var calibration = new ControllerCalibration();
            var wizard = new CalibrationWizard(calibration);
            wizard.Capture(Stick(512, 512), 0f, false);
            Assert.That(wizard.Capture(Stick(512, 512), float.NaN, false), Is.False);
            Assert.That(wizard.Error, Does.Contain("No reading"));
            Assert.That(wizard.Capture(Stick(512, 512), 1000f, true), Is.True);
            Assert.That(wizard.Capture(Stick(512, 512), 900f, true), Is.False);
            Assert.That(wizard.Error, Does.Contain("only 100 apart"));
            Assert.That(calibration.throttleMax, Is.EqualTo(1023), "unchanged after a rejected capture");
            Assert.That(wizard.Capture(Stick(512, 512), 20f, true), Is.True, "the 5 V end may be either one");
            Assert.That(calibration.throttleMin, Is.EqualTo(1000));
            Assert.That(calibration.throttleMax, Is.EqualTo(20));
        }
    }
}

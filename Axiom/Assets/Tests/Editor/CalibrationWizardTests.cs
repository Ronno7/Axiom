using GeometricFlight.Input;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class CalibrationWizardTests
    {
        private static ControllerPacket Stick(int x, int y) => new ControllerPacket(x, y, 150, true, false);

        [Test]
        public void FullRunWritesCenterAndThrottleRange()
        {
            var calibration = new ControllerCalibration();
            var wizard = new CalibrationWizard(calibration);
            Assert.That(wizard.Capture(Stick(498, 530), 0f, false), Is.True);
            Assert.That(wizard.Capture(Stick(512, 512), 55f, true), Is.True);
            Assert.That(calibration.throttleMinMm, Is.EqualTo(60), "range unchanged until both ends are captured");
            Assert.That(wizard.Capture(Stick(512, 512), 212.4f, true), Is.True);
            Assert.That(wizard.Current, Is.EqualTo(CalibrationWizard.Step.Check));
            Assert.That(calibration.stickCenterX, Is.EqualTo(498));
            Assert.That(calibration.stickCenterY, Is.EqualTo(530));
            Assert.That(calibration.throttleMinMm, Is.EqualTo(55));
            Assert.That(calibration.throttleMaxMm, Is.EqualTo(212));
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
        public void RejectsMissingEchoAndTooShortTravel()
        {
            var calibration = new ControllerCalibration();
            var wizard = new CalibrationWizard(calibration);
            wizard.Capture(Stick(512, 512), 0f, false);
            Assert.That(wizard.Capture(Stick(512, 512), float.NaN, false), Is.False);
            Assert.That(wizard.Error, Does.Contain("No echo"));
            Assert.That(wizard.Capture(Stick(512, 512), 100f, true), Is.True);
            Assert.That(wizard.Capture(Stick(512, 512), 115f, true), Is.False);
            Assert.That(wizard.Error, Does.Contain("15 mm"));
            Assert.That(calibration.throttleMaxMm, Is.EqualTo(300), "unchanged after a rejected capture");
            Assert.That(wizard.Capture(Stick(512, 512), 40f, true), Is.True, "maximum may be closer than minimum");
            Assert.That(calibration.throttleMinMm, Is.EqualTo(100));
            Assert.That(calibration.throttleMaxMm, Is.EqualTo(40));
        }
    }
}

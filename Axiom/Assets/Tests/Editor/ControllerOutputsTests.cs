using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ControllerOutputsTests
    {
        private static readonly Vector2 Multipliers = new Vector2(0.6f, 1.5f);

        // ---------- Speed to throttle ----------

        [TestCase(-1f)]
        [TestCase(-0.4f)]
        [TestCase(0f)]
        [TestCase(0.7f)]
        [TestCase(1f)]
        public void ThrottleForSpeedInvertsTargetSpeed(float throttle)
        {
            float speed = FlightMovement.TargetSpeed(throttle, 6f, Multipliers);
            Assert.That(FlightMovement.ThrottleForSpeed(speed, 6f, Multipliers), Is.EqualTo(throttle).Within(1e-4f));
        }

        // ---------- Runway lights ----------

        [Test]
        public void BandLightsSteadyWhenLeverIsInIt()
        {
            // Fast band 0.75-1 covers LEDs 3 and 4 when minimum throttle is nearest the sensor.
            byte bits = RunwayLights.Pattern(0.9f, new Vector2(0.75f, 1f), true, true, false, 0f);
            Assert.That(bits, Is.EqualTo(0b11000));
        }

        [Test]
        public void ReversedCalibrationMirrorsTheBand()
        {
            byte bits = RunwayLights.Pattern(0.9f, new Vector2(0.75f, 1f), false, true, false, 0f);
            Assert.That(bits, Is.EqualTo(0b00011));
        }

        [Test]
        public void ChaseRunsFromTheLeverTowardTheBand()
        {
            var band = new Vector2(0.75f, 1f);
            float step = RunwayLights.ChaseStepSeconds;
            // Lever at LED 0: the chase lights 0, 1, 2 in turn (never on the band itself), pauses, repeats.
            Assert.That(RunwayLights.Pattern(0.1f, band, true, false, false, 0.5f * step), Is.EqualTo(0b11001));
            Assert.That(RunwayLights.Pattern(0.1f, band, true, false, false, 1.5f * step), Is.EqualTo(0b11010));
            Assert.That(RunwayLights.Pattern(0.1f, band, true, false, false, 2.5f * step), Is.EqualTo(0b11100));
            Assert.That(RunwayLights.Pattern(0.1f, band, true, false, false, 3.5f * step), Is.EqualTo(0b11000));
            Assert.That(RunwayLights.Pattern(0.1f, band, true, false, false, 4.5f * step), Is.EqualTo(0b11001));
        }

        [Test]
        public void LeverNextToTheBandBlinks()
        {
            var band = new Vector2(0.75f, 1f);
            float step = RunwayLights.ChaseStepSeconds;
            Assert.That(RunwayLights.Pattern(0.5f, band, true, false, false, 0.5f * step), Is.EqualTo(0b11100));
            Assert.That(RunwayLights.Pattern(0.5f, band, true, false, false, 1.5f * step), Is.EqualTo(0b11000));
        }

        [Test]
        public void ApproachingZoneBlinksItsBand()
        {
            var slow = new Vector2(0f, 0.0625f);
            Assert.That(RunwayLights.Pattern(0.5f, slow, true, false, true, 0.05f), Is.EqualTo(0b00001));
            Assert.That(RunwayLights.Pattern(0.5f, slow, true, false, true, 0.25f), Is.EqualTo(0));
        }

        // ---------- Send policy ----------

        [Test]
        public void OutputsSendChangesKeepAliveAndGoDarkWhenNotRefreshed()
        {
            bool fanSetting = GameSettings.FanEnabled;
            GameSettings.FanEnabled = true;
            try
            {
                var outputs = new ControllerOutputs();
                outputs.Set(0b101, 128, 0.0);
                Assert.That(outputs.Next(0.0), Does.StartWith("LF,5,128*"));
                outputs.Set(0b101, 128, 0.1);
                Assert.That(outputs.Next(0.1), Is.Null, "unchanged");
                outputs.Set(0b101, 128, 0.55);
                Assert.That(outputs.Next(0.55), Does.StartWith("LF,5,128*"), "keep-alive");
                Assert.That(outputs.Next(0.9), Does.StartWith("LF,0,0*"), "nobody set them: off");

                GameSettings.FanEnabled = false;
                outputs.Set(0, 200, 1.0);
                Assert.That(outputs.Next(1.0), Is.Null, "fan muted in Options: still 0,0");
            }
            finally
            {
                GameSettings.FanEnabled = fanSetting;
            }
        }

        // ---------- Proximity alarm ----------

        [Test]
        public void BeepsQuickenAsImpactNears()
        {
            Assert.That(ProximityAlarm.BeepInterval(ProximityAlarm.Horizon), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ProximityAlarm.BeepInterval(0.6f), Is.LessThan(ProximityAlarm.BeepInterval(1f)));
            Assert.That(ProximityAlarm.BeepInterval(0.2f), Is.EqualTo(0f), "solid tone");
        }
    }
}

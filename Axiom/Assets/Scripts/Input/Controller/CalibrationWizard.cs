using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>
    /// Step logic for calibrating the controller, independent of UI: stick centre, then the throttle's
    /// minimum and maximum distances, then a check. Each Capture validates its reading and either
    /// advances or leaves an Error explaining what to fix. Throttle values are written to the
    /// calibration only once both ends are captured, so the mapping never sits half-changed.
    /// </summary>
    public sealed class CalibrationWizard
    {
        public enum Step { Center, ThrottleMin, ThrottleMax, Check }

        public const int MinThrottleTravelMm = 30;
        /// <summary>How far from the middle of the 0–1023 range a released stick may rest.</summary>
        public const int MaxCenterOffset = 200;

        private readonly ControllerCalibration target;
        private int capturedMinMm;

        public Step Current { get; private set; } = Step.Center;
        public string Error { get; private set; }

        public CalibrationWizard(ControllerCalibration target) => this.target = target;

        public void Restart()
        {
            Current = Step.Center;
            Error = null;
        }

        /// <param name="filteredMm">Smoothed sonar distance at the moment of capture.</param>
        /// <param name="throttleValid">False when the sensor currently has no echo.</param>
        /// <returns>True if the step was accepted and the wizard advanced.</returns>
        public bool Capture(ControllerPacket packet, float filteredMm, bool throttleValid)
        {
            Error = null;
            switch (Current)
            {
                case Step.Center:
                    if (Mathf.Abs(packet.StickX - 512) > MaxCenterOffset || Mathf.Abs(packet.StickY - 512) > MaxCenterOffset)
                    {
                        Error = "The stick isn't centered. Let go of it and try again.";
                        return false;
                    }
                    target.stickCenterX = packet.StickX;
                    target.stickCenterY = packet.StickY;
                    if (packet.IsCockpit && Mathf.Abs(packet.Twist - 512) <= MaxCenterOffset)
                        target.twistCenter = packet.Twist;
                    Current = Step.ThrottleMin;
                    return true;

                case Step.ThrottleMin:
                    if (!ThrottleReading(filteredMm, throttleValid, out capturedMinMm)) return false;
                    Current = Step.ThrottleMax;
                    return true;

                case Step.ThrottleMax:
                    if (!ThrottleReading(filteredMm, throttleValid, out int maxMm)) return false;
                    if (Mathf.Abs(maxMm - capturedMinMm) < MinThrottleTravelMm)
                    {
                        Error = $"Minimum and maximum are only {Mathf.Abs(maxMm - capturedMinMm)} mm apart. "
                              + "Slide the throttle all the way to its maximum and try again.";
                        return false;
                    }
                    target.throttleMinMm = capturedMinMm;
                    target.throttleMaxMm = maxMm;
                    Current = Step.Check;
                    return true;

                default:
                    return false;
            }
        }

        private bool ThrottleReading(float filteredMm, bool valid, out int mm)
        {
            mm = Mathf.RoundToInt(filteredMm);
            if (valid && !float.IsNaN(filteredMm)) return true;
            Error = "No echo from the throttle sensor. Check the card is square in front of it.";
            return false;
        }
    }
}

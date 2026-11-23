using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>
    /// Step logic for calibrating the controller, independent of UI: stick centre, then the throttle's
    /// minimum and maximum readings, then a check. Each Capture validates its reading and either
    /// advances or leaves an Error explaining what to fix. Throttle values are written to the
    /// calibration only once both ends are captured, so the mapping never sits half-changed.
    /// </summary>
    public sealed class CalibrationWizard
    {
        public enum Step { Center, ThrottleMin, ThrottleMax, Check }

        /// <summary>Fewest raw counts between the throttle's two ends (the fader's full travel is about 1023).</summary>
        public const int MinThrottleTravel = 200;
        /// <summary>How far from the middle of the 0–1023 range a released stick may rest.</summary>
        public const int MaxCenterOffset = 200;

        private readonly ControllerCalibration target;
        private int capturedMin;

        public Step Current { get; private set; } = Step.Center;
        public string Error { get; private set; }

        public CalibrationWizard(ControllerCalibration target) => this.target = target;

        public void Restart()
        {
            Current = Step.Center;
            Error = null;
        }

        /// <param name="filteredThrottle">Smoothed raw fader reading at the moment of capture.</param>
        /// <param name="throttleValid">False when the throttle currently has no readings.</param>
        /// <returns>True if the step was accepted and the wizard advanced.</returns>
        public bool Capture(ControllerPacket packet, float filteredThrottle, bool throttleValid)
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
                    if (Mathf.Abs(packet.Twist - 512) <= MaxCenterOffset)
                        target.twistCenter = packet.Twist;
                    Current = Step.ThrottleMin;
                    return true;

                case Step.ThrottleMin:
                    if (!ThrottleReading(filteredThrottle, throttleValid, out capturedMin)) return false;
                    Current = Step.ThrottleMax;
                    return true;

                case Step.ThrottleMax:
                    if (!ThrottleReading(filteredThrottle, throttleValid, out int max)) return false;
                    if (Mathf.Abs(max - capturedMin) < MinThrottleTravel)
                    {
                        Error = $"Minimum and maximum read only {Mathf.Abs(max - capturedMin)} apart. "
                              + "Slide the throttle all the way to its maximum and try again.";
                        return false;
                    }
                    target.throttleMin = capturedMin;
                    target.throttleMax = max;
                    Current = Step.Check;
                    return true;

                default:
                    return false;
            }
        }

        private bool ThrottleReading(float filtered, bool valid, out int raw)
        {
            raw = Mathf.RoundToInt(filtered);
            if (valid && !float.IsNaN(filtered)) return true;
            Error = "No reading from the throttle. Check the fader's cable on the A3 row.";
            return false;
        }
    }
}

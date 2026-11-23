using System;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>Per-controller calibration, saved as JSON in PlayerPrefs by ControllerService.</summary>
    [Serializable]
    public sealed class ControllerCalibration
    {
        public int stickCenterX = 512;
        public int stickCenterY = 512;
        public int stickMin = 0;
        public int stickMax = 1023;
        [Range(0f, 0.5f)] public float stickDeadzone = 0.12f;
        public bool invertStickX;
        public bool invertStickY;
        /// <summary>Twist rest position (cockpit only), captured with the stick centre.</summary>
        public int twistCenter = 512;
        [Range(0f, 0.5f)] public float twistDeadzone = 0.12f;
        public bool invertTwist;
        /// <summary>Raw fader readings at minimum and maximum throttle. Either order works, so it doesn't matter
        /// which end of the fader is wired to 5 V.</summary>
        public int throttleMin = 0;
        public int throttleMax = 1023;

        /// <summary>Raw axes to steering in the unit circle. Each half-axis is scaled separately, so an
        /// off-centre stick still reaches ±1; a radial deadzone removes rest jitter without a jump.</summary>
        public Vector2 NormalizeStick(int rawX, int rawY)
        {
            var v = new Vector2(Axis(rawX, stickCenterX, invertStickX), Axis(rawY, stickCenterY, invertStickY));
            float magnitude = v.magnitude;
            if (magnitude <= stickDeadzone) return Vector2.zero;
            float scaled = Mathf.Clamp01((magnitude - stickDeadzone) / (1f - stickDeadzone));
            return v / magnitude * scaled;
        }

        /// <summary>Raw twist to -1…+1. Twist pots drift and rest unevenly, so it gets its own, larger
        /// deadzone, scaled so output starts from zero at the deadzone edge.</summary>
        public float NormalizeTwist(int raw)
        {
            float value = Axis(raw, twistCenter, invertTwist);
            float magnitude = Mathf.Abs(value);
            if (magnitude <= twistDeadzone) return 0f;
            return Mathf.Sign(value) * Mathf.Clamp01((magnitude - twistDeadzone) / (1f - twistDeadzone));
        }

        /// <summary>Fader reading to a throttle setpoint: -1 at the minimum position, +1 at the maximum.</summary>
        public float ThrottleFromRaw(float raw)
        {
            if (throttleMax == throttleMin) return 0f;
            return Mathf.InverseLerp(throttleMin, throttleMax, raw) * 2f - 1f;
        }

        private float Axis(int raw, int center, bool invert)
        {
            float span = raw >= center ? stickMax - center : center - stickMin;
            float value = span > 0f ? Mathf.Clamp((raw - center) / span, -1f, 1f) : 0f;
            return invert ? -value : value;
        }
    }

    /// <summary>
    /// Turns raw fader readings into a steady throttle setpoint: a median of the last three rejects a
    /// single wiper glitch, and short exponential smoothing removes the last count of jitter. Brief gaps
    /// in the packets hold the last value; after LossTimeout without a reading the signal counts as lost
    /// and the setpoint eases to cruise (0), a safe middle, until readings return.
    /// </summary>
    public sealed class ThrottleFilter
    {
        public float SmoothingTime = 0.05f;
        public float LossTimeout = 0.5f;
        public float LossEaseRate = 1f;

        private readonly float[] window = new float[3];
        private int samples;
        private int next;
        private float target = float.NaN;
        private float filtered = float.NaN;
        private float sinceValid = float.PositiveInfinity;

        public float Setpoint { get; private set; }
        public bool SignalLost => sinceValid > LossTimeout;
        /// <summary>The smoothed raw reading, 0–1023 (NaN before the first).</summary>
        public float Filtered => filtered;

        public void Reset()
        {
            samples = next = 0;
            target = filtered = float.NaN;
            sinceValid = float.PositiveInfinity;
            Setpoint = 0f;
        }

        /// <param name="newSample">True when a packet arrived since the last update.</param>
        /// <param name="raw">That packet's fader reading, 0–1023.</param>
        public void Update(float deltaTime, bool newSample, int raw, ControllerCalibration calibration)
        {
            if (deltaTime > 0f) sinceValid += deltaTime;
            if (newSample && raw >= 0 && raw <= 1023)
            {
                // After a loss, readings from before it are stale; start the median fresh.
                if (SignalLost) samples = next = 0;
                window[next] = raw;
                next = (next + 1) % window.Length;
                samples = Mathf.Min(samples + 1, window.Length);
                target = Median();
                if (float.IsNaN(filtered) || SignalLost) filtered = target;
                sinceValid = 0f;
            }

            if (SignalLost || float.IsNaN(filtered))
            {
                Setpoint = Mathf.MoveTowards(Setpoint, 0f, LossEaseRate * Mathf.Max(0f, deltaTime));
                return;
            }
            float alpha = deltaTime > 0f ? 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.001f, SmoothingTime)) : 0f;
            filtered = Mathf.Lerp(filtered, target, alpha);
            Setpoint = calibration.ThrottleFromRaw(filtered);
        }

        private float Median()
        {
            if (samples == 1) return window[(next + 2) % 3];
            if (samples == 2) return (window[0] + window[1]) * 0.5f;
            float a = window[0], b = window[1], c = window[2];
            return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
        }
    }
}

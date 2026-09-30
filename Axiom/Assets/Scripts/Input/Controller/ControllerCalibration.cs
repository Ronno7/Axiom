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
        /// <summary>Sensor distance at minimum and maximum throttle. Either order works.</summary>
        public int throttleMinMm = 60;
        public int throttleMaxMm = 300;

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

        /// <summary>Distance to a throttle setpoint: -1 at the minimum position, +1 at the maximum.</summary>
        public float ThrottleFromDistance(float millimetres)
        {
            if (throttleMaxMm == throttleMinMm) return 0f;
            return Mathf.InverseLerp(throttleMinMm, throttleMaxMm, millimetres) * 2f - 1f;
        }

        private float Axis(int raw, int center, bool invert)
        {
            float span = raw >= center ? stickMax - center : center - stickMin;
            float value = span > 0f ? Mathf.Clamp((raw - center) / span, -1f, 1f) : 0f;
            return invert ? -value : value;
        }
    }

    /// <summary>
    /// Turns raw sonar samples into a steady throttle setpoint:
    /// out-of-range readings are discarded, a median of the last three rejects single spikes, and
    /// exponential smoothing removes jitter. Brief dropouts hold the last value; after
    /// LossTimeout without a valid reading the signal counts as lost and the setpoint eases to
    /// cruise (0), a safe middle, until readings return.
    /// </summary>
    public sealed class ThrottleFilter
    {
        public float MinValidMm = 20f;
        public float MaxValidMm = 1200f;
        public float SmoothingTime = 0.08f;
        public float LossTimeout = 0.5f;
        public float LossEaseRate = 1f;

        private readonly float[] window = new float[3];
        private int samples;
        private int next;
        private float targetMm = float.NaN;
        private float filteredMm = float.NaN;
        private float sinceValid = float.PositiveInfinity;

        public float Setpoint { get; private set; }
        public bool SignalLost => sinceValid > LossTimeout;
        public float FilteredMm => filteredMm;

        public void Reset()
        {
            samples = next = 0;
            targetMm = filteredMm = float.NaN;
            sinceValid = float.PositiveInfinity;
            Setpoint = 0f;
        }

        /// <param name="newSample">True when a packet arrived since the last update.</param>
        /// <param name="distanceMm">That packet's distance (-1 for no echo).</param>
        public void Update(float deltaTime, bool newSample, int distanceMm, ControllerCalibration calibration)
        {
            if (deltaTime > 0f) sinceValid += deltaTime;
            if (newSample && distanceMm >= MinValidMm && distanceMm <= MaxValidMm)
            {
                // After a loss, readings from before it are stale; start the median fresh.
                if (SignalLost) samples = next = 0;
                window[next] = distanceMm;
                next = (next + 1) % window.Length;
                samples = Mathf.Min(samples + 1, window.Length);
                targetMm = Median();
                if (float.IsNaN(filteredMm) || SignalLost) filteredMm = targetMm;
                sinceValid = 0f;
            }

            if (SignalLost || float.IsNaN(filteredMm))
            {
                Setpoint = Mathf.MoveTowards(Setpoint, 0f, LossEaseRate * Mathf.Max(0f, deltaTime));
                return;
            }
            float alpha = deltaTime > 0f ? 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.001f, SmoothingTime)) : 0f;
            filteredMm = Mathf.Lerp(filteredMm, targetMm, alpha);
            Setpoint = calibration.ThrottleFromDistance(filteredMm);
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

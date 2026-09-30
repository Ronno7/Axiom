using GeometricFlight.Core;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>
    /// The controller's lights and fan. The level sets them every frame; anything not refreshed for
    /// Expiry goes dark and still, so menus, pauses and scene loads never leave the fan running.
    /// Sent when changed (at most every SendInterval) and every KeepAlive; the firmware stops the
    /// fan by itself if the keep-alives stop, e.g. when the game quits.
    /// </summary>
    public sealed class ControllerOutputs
    {
        public const double SendInterval = 0.05;
        public const double KeepAlive = 0.5;
        public const double Expiry = 0.25;

        private byte lights, fan;
        private double setAt = double.NegativeInfinity;
        private int sentLights = -1, sentFan = -1;
        private double sentAt = double.NegativeInfinity;

        /// <param name="lightBits">Bits 0-4 runway LEDs (bit 0 nearest the sensor), 5-7 underglow R, G, B.</param>
        /// <param name="fanLevel">0 still, 255 full; muted by the Options fan switch.</param>
        public void Set(byte lightBits, byte fanLevel, double now)
        {
            lights = lightBits;
            fan = GameSettings.FanEnabled ? fanLevel : (byte)0;
            setAt = now;
        }

        public void Invalidate()
        {
            sentLights = sentFan = -1;
            sentAt = double.NegativeInfinity;
        }

        /// <summary>The framed command to send now, or null.</summary>
        public string Next(double now)
        {
            if (now - sentAt < SendInterval) return null;
            bool live = now - setAt <= Expiry;
            int l = live ? lights : 0, f = live ? fan : 0;
            if (l == sentLights && f == sentFan && now - sentAt < KeepAlive) return null;
            sentLights = l;
            sentFan = f;
            sentAt = now;
            return ControllerProtocol.FormatOutputs(l, f);
        }
    }

    /// <summary>
    /// Pattern for the five LEDs beside the throttle slot. They are evenly spaced between the slot's
    /// ends, LED 0 nearest the sensor, so each covers a fifth of the lever's travel. Inside a throttle
    /// zone the LEDs over the recommended band light steadily and, while the lever is outside it, a
    /// light runs from the lever toward the band, like approach lights leading to a runway. Just
    /// before a zone the band blinks, so the lever can move early.
    /// </summary>
    public static class RunwayLights
    {
        public const int Count = 5;
        public const float ChaseStepSeconds = 0.09f;

        /// <param name="lever">Lever position, 0 (minimum throttle) to 1 (maximum).</param>
        /// <param name="band">Recommended band in the same units.</param>
        /// <param name="minNearSensor">True when minimum throttle is the end nearest the sensor (from calibration).</param>
        /// <param name="inBand">Whether the throttle currently asks for a speed inside the band.</param>
        /// <param name="approaching">A zone is about to start: blink its band instead.</param>
        public static byte Pattern(float lever, Vector2 band, bool minNearSensor, bool inBand, bool approaching,
            float time)
        {
            // Lever units to slot position, 0 at the sensor end.
            float lo = minNearSensor ? band.x : 1f - band.y;
            float hi = minNearSensor ? band.y : 1f - band.x;
            float position = minNearSensor ? lever : 1f - lever;

            int first = Index(lo), last = Index(hi);
            byte bandBits = 0;
            for (int i = first; i <= last; i++) bandBits |= (byte)(1 << i);

            if (approaching) return time % 0.33f < 0.165f ? bandBits : (byte)0;
            int at = Index(position);
            if (inBand || (at >= first && at <= last)) return bandBits;

            // One light runs from the lever's LED up to (not onto) the band, pauses a beat, and starts
            // over. The pause keeps the direction readable and makes a one-step chase a blink.
            int target = at < first ? first : last;
            int steps = Mathf.Abs(target - at);
            int step = Mathf.FloorToInt(time / ChaseStepSeconds) % (steps + 1);
            if (step == steps) return bandBits;
            int chase = at + (target > at ? step : -step);
            return (byte)(bandBits | (1 << chase));
        }

        /// <summary>The LED whose fifth of the slot contains <paramref name="position"/>.</summary>
        public static int Index(float position) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(position) * Count), 0, Count - 1);
    }
}

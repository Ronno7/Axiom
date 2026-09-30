using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.UI
{
    /// <summary>
    /// Collision warning in the style of a car's parking sensor: the ship's path is projected ahead
    /// (current speed and steering, within the flight bounds) and the buzzer beeps faster as the
    /// first scenery contact gets closer, becoming a solid tone just before it. Targets are ignored:
    /// flying at one to shoot it is the point. Pulse is on in time with each beep for the matching
    /// visual cue, which also covers a muted buzzer or keyboard play.
    /// </summary>
    public sealed class ProximityAlarm
    {
        public const float Horizon = 1.2f;
        public const float SampleStep = 0.1f;
        public const float SolidBelow = 0.25f;
        private const int ToneHz = 1760;

        public bool Active { get; private set; }
        public bool Pulse { get; private set; }

        private float beepStart = float.NegativeInfinity;
        private float nextBeep;

        /// <summary>Seconds between beeps for a contact <paramref name="timeToImpact"/> away; 0 = solid tone.</summary>
        public static float BeepInterval(float timeToImpact) => timeToImpact <= SolidBelow
            ? 0f : Mathf.Lerp(0.1f, 0.5f, Mathf.InverseLerp(SolidBelow, Horizon, timeToImpact));

        public void Update(PlayerFlight flight, ShipHull hull, bool enabled, float now)
        {
            float impact = enabled ? TimeToImpact(flight, hull) : float.PositiveInfinity;
            Active = impact <= Horizon;
            if (!Active)
            {
                Pulse = false;
                nextBeep = now;
                return;
            }

            float interval = BeepInterval(impact);
            if (interval <= 0f)
            {
                Pulse = true;
                if (now >= nextBeep)
                {
                    ControllerService.Instance.PlayTone(ToneHz, 200);
                    nextBeep = now + 0.15f;
                }
                return;
            }
            nextBeep = Mathf.Min(nextBeep, beepStart + interval); // Closing in: don't wait out a slower beat.
            if (now >= nextBeep)
            {
                ControllerService.Instance.PlayTone(ToneHz, 50);
                beepStart = now;
                nextBeep = now + interval;
            }
            Pulse = now - beepStart < interval * 0.5f;
        }

        private static float TimeToImpact(PlayerFlight flight, ShipHull hull)
        {
            // Already inside something: the hit has happened; warning now would only be noise.
            if (hull.Overlaps(flight.transform.position, false)) return float.PositiveInfinity;
            for (float t = SampleStep; t <= Horizon + 0.001f; t += SampleStep)
                if (hull.Overlaps(flight.PredictPosition(t), false)) return t;
            return float.PositiveInfinity;
        }
    }
}

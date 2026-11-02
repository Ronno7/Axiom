using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.UI
{
    /// <summary>
    /// The throttle gauge is the lever's travel: the cruise detent in the centre, normal full throttle at the
    /// boost gate and burst speeds across the gate's final stretch. Speeds are drawn on the same travel, so a
    /// steady speed sits under the lever that asks for it. Shared by the HUD and the cockpit display.
    /// </summary>
    public static class ThrottleGauge
    {
        /// <summary>Matches the cockpit's eight throttle lights.</summary>
        public const int Segments = 8;

        /// <summary>Gauge position (0–1) of a lever setpoint (-1…+1).</summary>
        public static float LeverFraction(float lever) => Mathf.Clamp01((lever + 1f) * 0.5f);

        public static float SpeedFraction(ShipFlight flight, float speed) => SpeedFraction(speed, flight.MinSpeed,
            flight.CruiseSpeed, flight.MaxSpeed, flight.BoostSpeed, flight.BoostGate);

        /// <summary>Gauge position (0–1) of a speed: min→cruise over the back half, cruise→max up to the gate,
        /// max→boost speed beyond it.</summary>
        public static float SpeedFraction(float speed, float min, float cruise, float max, float boost, float gate)
        {
            float lever;
            if (speed <= cruise) lever = -Mathf.InverseLerp(cruise, min, speed);
            else if (speed <= max || boost <= max) lever = gate * Mathf.InverseLerp(cruise, max, speed);
            else lever = gate + (1f - gate) * Mathf.InverseLerp(max, boost, speed);
            return LeverFraction(lever);
        }

        /// <summary>LOW, CRUISE, FULL or BOOST; empty between cruise and full.</summary>
        public static string Word(ShipFlight flight)
        {
            if (flight.Boost == BoostStatus.Active) return "BOOST";
            float lever = flight.Throttle;
            if (lever < 0f) return "LOW";
            if (lever == 0f) return "CRUISE";
            return lever >= Mathf.Min(flight.BoostGate, 1f) - 0.005f ? "FULL" : string.Empty;
        }

        public static string ReserveText(BoostStatus status)
        {
            switch (status)
            {
                case BoostStatus.Active: return "ACTIVE";
                case BoostStatus.Charging: return "CHARGING";
                case BoostStatus.PullBack: return "PULL BACK";
                case BoostStatus.Ready: return "READY";
                default: return string.Empty;
            }
        }
    }
}

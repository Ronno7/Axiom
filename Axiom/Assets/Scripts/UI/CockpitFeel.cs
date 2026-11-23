using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.UI
{
    /// <summary>
    /// The cockpit's rumble standing in for the notches a fader doesn't have: a short tick when the lever settles
    /// into cruise, and a buzz when a burst fires. Without a cockpit connected the pulses go nowhere.
    /// </summary>
    public sealed class CockpitFeel
    {
        public const int TickLevel = 200, TickMilliseconds = 40;
        public const int BurstLevel = 255, BurstMilliseconds = 220;
        /// <summary>How far past the cruise notch the lever must go before the next tick, so a lever resting on the
        /// notch's edge doesn't chatter.</summary>
        public const float Rearm = 0.03f;

        private readonly ShipFlight flight;
        private bool cruiseArmed;
        private bool wasBursting;

        public CockpitFeel(ShipFlight flight) => this.flight = flight;

        /// <param name="flying">The run is playing and the player has the ship.</param>
        public void Tick(bool flying)
        {
            bool bursting = flight.Boost == BoostStatus.Active;
            if (flying)
            {
                if (CruiseClick(ref cruiseArmed, flight.CurrentInput.Throttle, flight.Tuning.cruiseSnap))
                    ControllerService.Current?.PulseRumble(TickLevel, TickMilliseconds);
                if (bursting && !wasBursting) ControllerService.Current?.PulseRumble(BurstLevel, BurstMilliseconds);
            }
            else cruiseArmed = false;
            wasBursting = bursting;
        }

        /// <summary>True once each time the lever (raw, -1…+1) comes into the cruise notch from clearly outside it.</summary>
        public static bool CruiseClick(ref bool armed, float lever, float notch)
        {
            float magnitude = Mathf.Abs(lever);
            if (magnitude > notch + Rearm) armed = true;
            else if (magnitude <= notch && armed)
            {
                armed = false;
                return true;
            }
            return false;
        }
    }
}

using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>What weapons and presentation need from whichever component flies the ship: the rail
    /// course (PlayerFlight) or free flight in an arena (ArenaFlight). Each reads its input source once
    /// per Update and exposes that snapshot here, so nothing else reads the input a second time.</summary>
    public abstract class ShipFlight : MonoBehaviour
    {
        /// <summary>Raw input snapshot for this frame (before player options are applied).</summary>
        public FlightInput CurrentInput { get; protected set; }
        public abstract float CurrentSpeed { get; }
        /// <summary>World velocity this frame; shots inherit it.</summary>
        public abstract Vector3 Velocity { get; }
        public abstract float CruiseSpeed { get; }
        public abstract float MinSpeed { get; }
        public abstract float MaxSpeed { get; }
        public abstract float TargetSpeed { get; }
        /// <summary>When locked, input is ignored: no steering, cruise throttle, no fire or pause.</summary>
        public bool InputLocked { get; set; }
    }
}

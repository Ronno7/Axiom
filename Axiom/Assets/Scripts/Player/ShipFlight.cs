using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>What weapons and presentation need from the component that flies the ship. It reads its input source
    /// once per Update and exposes that snapshot here, so nothing else reads the input a second time.</summary>
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
        /// <summary>The throttle as the gauge shows it, -1 to +1 with the detent at 0.</summary>
        public virtual float Lever => CurrentInput.Throttle;
        /// <summary>Forward lever position where normal full throttle ends and the boost gate begins
        /// (1 = no boost).</summary>
        public virtual float BoostGate => 1f;
        public virtual float BoostSpeed => MaxSpeed;
        public virtual BoostStatus Boost => BoostStatus.Off;
        /// <summary>0 = empty, 1 = full.</summary>
        public virtual float BoostCharge => 0f;
        /// <summary>When locked, input is ignored: no steering, cruise throttle, no fire or pause.</summary>
        public bool InputLocked { get; set; }
        /// <summary>Whether <see cref="PredictPosition"/> models this ship's current motion honestly
        /// enough for collision warnings.</summary>
        public virtual bool CanPredict => false;
        /// <summary>Where the ship would be after <paramref name="seconds"/> with this frame's input held.
        /// Never changes the ship's actual state.</summary>
        public virtual Vector3 PredictPosition(float seconds) => transform.position;
    }
}

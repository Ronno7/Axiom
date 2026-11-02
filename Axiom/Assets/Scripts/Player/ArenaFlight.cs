using System;
using GeometricFlight.Core;
using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Flies the ship: the nose-led rail (RailFlightModel) up to the rail's end, then free flight in the arena
    /// (ArenaFlightModel). The stick rolls and pitches or banks and turns, the twist is a rudder, the throttle sets speed
    /// and how hard the ship turns. Reads its input source once per Update.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaFlight : ShipFlight
    {
        [SerializeField] private FlightInputSource inputSource;
        [SerializeField] private ArenaBounds arena;
        [SerializeField] private ArenaFlightTuning tuning = new ArenaFlightTuning();
        [Tooltip("Push the stick forward to dive, as in an aircraft. Off: forward climbs, like the rail course. "
               + "The Options invert setting flips either.")]
        [SerializeField] private bool pushToDive = true;
        [Tooltip("Full rotation: free loops, rolls and inverted flight; turning is roll-then-pull. Off: arcade "
               + "bank-and-turn that keeps the ship upright.")]
        [SerializeField] private bool fullRotation = true;
        [Tooltip("World Z where the rail ends and the arena's flight takes over.")]
        [SerializeField] private float railEndZ = 900f;
        [Tooltip("How far the nose can turn from straight ahead on the rail. Wider cones need a separate time-integration "
               + "check before use.")]
        [SerializeField, Range(40f, 55f)] private float railHeadingLimit = 45f;

        private ArenaFlightState state;
        private ArenaFlightState spawn;

        /// <summary>The ship flew between these two points along the rail this frame.</summary>
        public event Action<Vector3, Vector3> RailFlown;
        public ArenaFlightState State => state;
        public ArenaFlightTuning Tuning => tuning;
        public bool FullRotation => state.FullRotation;
        public float RailHeadingLimit => railHeadingLimit;
        /// <summary>The ship has crossed the rail's end. Nothing else changes at that moment: no input or weapon reset.</summary>
        public bool RailReleased { get; private set; }
        /// <summary>Entry presentation owns the pose; input remains current without advancing flight.</summary>
        public bool EntryHeld { get; private set; }
        public int InputSampleFrame { get; private set; } = -1;
        /// <summary>This frame's throttle after the cruise notch, -1 to +1.</summary>
        public float Throttle { get; private set; }
        public override float CurrentSpeed => state.Speed;
        public override Vector3 Velocity => state.Forward * state.Speed;
        public override float CruiseSpeed => SpeedAt(0f);
        public override float MinSpeed => SpeedAt(-1f);
        public override float MaxSpeed => SpeedAt(1f);
        public override float TargetSpeed => state.Boost.Active ? tuning.boost.speed
            : SpeedAt(ThrottleBoost.Lever(Throttle, tuning.boost));
        public override float Lever => Throttle;
        public override float BoostGate => tuning.boost.enabled ? tuning.boost.gate : 1f;
        public override float BoostSpeed => tuning.boost.enabled ? tuning.boost.speed : MaxSpeed;
        public override BoostStatus Boost => ThrottleBoost.Status(state.Boost, tuning.boost);
        public override float BoostCharge => tuning.boost.enabled ? state.Boost.Charge : 0f;

        private void Awake()
        {
            if (inputSource == null || arena == null)
            {
                Debug.LogError("Assign a FlightInputSource and an ArenaBounds to ArenaFlight before playing.", this);
                enabled = false;
                return;
            }
            Vector3 forward = transform.forward;
            state = new ArenaFlightState
            {
                Position = transform.position,
                Yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                Speed = CruiseSpeed,
                Boost = BoostState.Full,
            };
            state.SetFullRotation(fullRotation);
            transform.rotation = state.Rotation;
            spawn = state;
        }

        /// <summary>The starting heading and cruise speed at <paramref name="position"/> (a checkpoint). The boost
        /// keeps its charge; the lever must come back before it fires.</summary>
        public void Respawn(Vector3 position)
        {
            BoostState boost = state.Boost;
            bool fullRotation = state.FullRotation;
            state = spawn;
            state.SetFullRotation(fullRotation); // Keep the handling mode the player is flying.
            state.Position = position;
            state.Boost = boost;
            state.Boost.Remaining = 0f;
            state.Boost.Armed = false;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
        }

        public void HoldForEntry() => EntryHeld = true;

        /// <summary>Stages the complete actor state during entry. After admission presentation can no
        /// longer reposition the ship through this API. Input and weapon authority are never reset here.</summary>
        public void StageEntry(ArenaFlightState entryState)
        {
            if (!EntryHeld) return;
            BoostState boost = state.Boost;
            state = entryState;
            state.Boost = boost;
            fullRotation = state.FullRotation;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
        }

        internal void ReleaseEntry() => EntryHeld = false;

        /// <summary>Rails only: the arena's own edge and floor behavior is not a scenery prediction.</summary>
        public override bool CanPredict => !EntryHeld && !RailReleased;

        /// <summary>Steps the pure rail model on a copy of the state with this frame's input held.</summary>
        public override Vector3 PredictPosition(float seconds)
        {
            if (!CanPredict || seconds <= 0f) return transform.position;
            ArenaFlightState predicted = state;
            Vector2 stick = ApplyOptions(CurrentInput.Steering);
            const float step = 0.05f;
            for (float t = 0f; t < seconds - 1e-4f; t += step)
                RailFlightModel.Step(ref predicted, stick, CurrentInput.Twist, Throttle, tuning, railHeadingLimit,
                    Mathf.Min(step, seconds - t));
            return predicted.Position;
        }

        private Vector2 ApplyOptions(Vector2 stick)
        {
            if (pushToDive) stick.y = -stick.y;
            if (GameSettings.InvertVertical) stick.y = -stick.y;
            stick.x *= GameSettings.SteeringSensitivity;
            stick.y *= GameSettings.SteeringSensitivity;
            return stick;
        }

        private void Update() => Step(Time.deltaTime);

        private void Step(float deltaTime)
        {
            CurrentInput = !InputLocked && inputSource != null && inputSource.isActiveAndEnabled
                ? inputSource.ReadInput(deltaTime) : default;
            InputSampleFrame = Time.frameCount;
            Throttle = ArenaFlightModel.SnapCruise(CurrentInput.Throttle, tuning.cruiseSnap);
            // Held entry and pause disarm boost: a lever parked forward never fires on hand-over or resume.
            if (EntryHeld || deltaTime <= 0f) ThrottleBoost.Step(ref state.Boost, Throttle, tuning.boost, 0f);
            if (EntryHeld) return;

            Vector2 stick = ApplyOptions(CurrentInput.Steering);
            bool wasBoosting = state.Boost.Active;
            Fly(stick, deltaTime);
            // A spent burst returns a latched (keyboard) throttle to normal full, out of the boost gate.
            if (wasBoosting && !state.Boost.Active && Throttle > tuning.boost.gate
                && inputSource.LimitThrottle(ThrottleBoost.RetractSetpoint(tuning.boost, tuning.cruiseSnap)))
                ThrottleBoost.Retracted(ref state.Boost);
            transform.SetPositionAndRotation(state.Position, state.Rotation);
        }

        private void Fly(Vector2 stick, float deltaTime)
        {
            if (RailReleased)
            {
                ArenaFlightModel.Fly(ref state, stick, CurrentInput.Twist, Throttle, tuning, arena.Limits, deltaTime);
                return;
            }
            Vector3 from = state.Position;
            RailReleased = RailFlightModel.FlyToRelease(ref state, stick, CurrentInput.Twist, Throttle, tuning,
                railHeadingLimit, railEndZ, arena.Limits, deltaTime, out Vector3 railEnd);
            RailFlown?.Invoke(from, railEnd);
        }

        private float SpeedAt(float throttle) =>
            FlightMovement.TargetSpeed(throttle, tuning.cruiseSpeed, tuning.throttleMultipliers);
    }
}

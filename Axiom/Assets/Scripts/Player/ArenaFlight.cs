using GeometricFlight.Core;
using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Free flight inside an arena (ArenaFlightModel): the stick banks and turns, the twist is a rudder,
    /// the throttle sets speed and how hard the ship turns. Reads its input source once per Update.</summary>
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
               + "bank-and-turn that keeps the ship upright. M switches in Play Mode.")]
        [SerializeField] private bool fullRotation = true;

        private ArenaFlightState state;
        private ArenaFlightState spawn;

        public ArenaFlightState State => state;
        public ArenaFlightTuning Tuning => tuning;
        public bool FullRotation => state.FullRotation;
        /// <summary>This frame's throttle after the cruise notch, -1 to +1.</summary>
        public float Throttle { get; private set; }
        public override float CurrentSpeed => state.Speed;
        public override Vector3 Velocity => state.Forward * state.Speed;
        public override float CruiseSpeed => SpeedAt(0f);
        public override float MinSpeed => SpeedAt(-1f);
        public override float MaxSpeed => SpeedAt(1f);
        public override float TargetSpeed => SpeedAt(Throttle);

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
            };
            state.SetFullRotation(fullRotation);
            transform.rotation = state.Rotation;
            spawn = state;
        }

        /// <summary>Switches between full rotation and arcade flight mid-flight.</summary>
        public void SetFullRotation(bool full)
        {
            fullRotation = full;
            state.SetFullRotation(full);
        }

        /// <summary>Back to the starting position, heading and cruise speed.</summary>
        public void Respawn()
        {
            state = spawn;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
        }

        private void Update()
        {
            CurrentInput = !InputLocked && inputSource != null && inputSource.isActiveAndEnabled
                ? inputSource.ReadInput(Time.deltaTime) : default;
            Vector2 stick = CurrentInput.Steering;
            if (pushToDive) stick.y = -stick.y;
            if (GameSettings.InvertVertical) stick.y = -stick.y;
            stick.x *= GameSettings.SteeringSensitivity;
            stick.y *= GameSettings.SteeringSensitivity;
            Throttle = ArenaFlightModel.SnapCruise(CurrentInput.Throttle, tuning.cruiseSnap);
            if (state.FullRotation)
                ArenaFlightModel.StepFull(ref state, stick, CurrentInput.Twist, Throttle, tuning, arena.Limits,
                    Time.deltaTime);
            else
                ArenaFlightModel.Step(ref state, stick, CurrentInput.Twist, Throttle, tuning, arena.Limits,
                    Time.deltaTime);
            transform.SetPositionAndRotation(state.Position, state.Rotation);
        }

        private float SpeedAt(float throttle) =>
            FlightMovement.TargetSpeed(throttle, tuning.cruiseSpeed, tuning.throttleMultipliers);
    }
}

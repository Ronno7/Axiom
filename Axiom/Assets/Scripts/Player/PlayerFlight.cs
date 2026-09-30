using GeometricFlight.Core;
using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerFlight : MonoBehaviour
    {
        [SerializeField] private FlightInputSource inputSource;
        [Tooltip("Cruising speed in world units per second.")]
        [SerializeField, Min(0.01f)] private float forwardSpeed = 6f;
        [Tooltip("Minimum and maximum speed relative to cruise. Minimum must be positive.")]
        [SerializeField] private Vector2 throttleMultipliers = new Vector2(0.6f, 1.5f);
        [SerializeField, Min(0.01f)] private float acceleration = 6f;
        [SerializeField, Min(0f)] private float steeringSpeed = 5f;
        [Tooltip("Minimum and maximum X offsets from the starting position, in world units.")]
        [SerializeField] private Vector2 horizontalLimits = new Vector2(-6f, 6f);
        [Tooltip("Minimum and maximum Y offsets from the starting position, in world units.")]
        [SerializeField] private Vector2 verticalLimits = new Vector2(-2f, 4f);

        private Vector3 startPosition;
        private float currentSpeed;
        public float CurrentSpeed => currentSpeed;
        /// <summary>Raw input snapshot for this frame (before player options are applied).</summary>
        public FlightInput CurrentInput { get; private set; }
        public float CruiseSpeed => SpeedAt(0f);
        public float MinSpeed => SpeedAt(-1f);
        public float MaxSpeed => SpeedAt(1f);
        public float TargetSpeed => SpeedAt(CurrentInput.Throttle);
        /// <summary>When locked, input is ignored: no steering, cruise throttle, no fire or pause.</summary>
        public bool InputLocked { get; set; }

        private void Awake()
        {
            startPosition = transform.position;
            currentSpeed = CruiseSpeed;
            if (inputSource == null)
            {
                Debug.LogError("Assign a FlightInputSource to PlayerFlight before playing.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            CurrentInput = !InputLocked && inputSource != null && inputSource.isActiveAndEnabled
                ? inputSource.ReadInput(Time.deltaTime) : default;
            float averageSpeed = FlightMovement.Accelerate(ref currentSpeed, TargetSpeed, acceleration, Time.deltaTime);
            Vector2 steering = CurrentInput.Steering;
            if (GameSettings.InvertVertical) steering.y = -steering.y;
            transform.position = FlightMovement.Step(transform.position, steering, Time.deltaTime,
                averageSpeed, steeringSpeed * GameSettings.SteeringSensitivity, GetBounds(startPosition));
        }

        public float ThrottleForSpeed(float speed) =>
            FlightMovement.ThrottleForSpeed(speed, forwardSpeed, throttleMultipliers);

        /// <summary>Where the ship will be after <paramref name="seconds"/> if the current speed and
        /// steering hold, clamped to the flight bounds like real movement.</summary>
        public Vector3 PredictPosition(float seconds)
        {
            Vector2 steering = CurrentInput.Steering;
            if (GameSettings.InvertVertical) steering.y = -steering.y;
            return FlightMovement.Step(transform.position, steering, seconds, currentSpeed,
                steeringSpeed * GameSettings.SteeringSensitivity, GetBounds(startPosition));
        }

        private float SpeedAt(float throttle) => FlightMovement.TargetSpeed(throttle, forwardSpeed, throttleMultipliers);

        private Rect GetBounds(Vector3 origin)
        {
            return Rect.MinMaxRect(
                origin.x + Mathf.Min(horizontalLimits.x, horizontalLimits.y),
                origin.y + Mathf.Min(verticalLimits.x, verticalLimits.y),
                origin.x + Mathf.Max(horizontalLimits.x, horizontalLimits.y),
                origin.y + Mathf.Max(verticalLimits.x, verticalLimits.y));
        }

        private void OnDrawGizmosSelected()
        {
            Rect bounds = GetBounds(Application.isPlaying ? startPosition : transform.position);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(new Vector3(bounds.center.x, bounds.center.y, transform.position.z),
                new Vector3(bounds.width, bounds.height, 0f));
        }
    }
}

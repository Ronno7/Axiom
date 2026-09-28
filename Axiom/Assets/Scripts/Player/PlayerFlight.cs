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
        public FlightInput CurrentInput { get; private set; }

        private void Awake()
        {
            startPosition = transform.position;
            currentSpeed = FlightMovement.TargetSpeed(0f, forwardSpeed, throttleMultipliers);
            if (inputSource == null)
            {
                Debug.LogError("Assign a FlightInputSource to PlayerFlight before playing.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            CurrentInput = inputSource != null && inputSource.isActiveAndEnabled
                ? inputSource.ReadInput(Time.deltaTime) : default;
            float targetSpeed = FlightMovement.TargetSpeed(CurrentInput.Throttle, forwardSpeed, throttleMultipliers);
            float averageSpeed = FlightMovement.Accelerate(ref currentSpeed, targetSpeed, acceleration, Time.deltaTime);
            transform.position = FlightMovement.Step(transform.position, CurrentInput.Steering, Time.deltaTime,
                averageSpeed, steeringSpeed, GetBounds(startPosition));
        }

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

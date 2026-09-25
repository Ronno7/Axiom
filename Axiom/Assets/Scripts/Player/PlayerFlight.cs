using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerFlight : MonoBehaviour
    {
        [SerializeField] private FlightInputSource inputSource;
        [SerializeField, Min(0f)] private float forwardSpeed = 6f;
        [SerializeField, Min(0f)] private float steeringSpeed = 5f;
        [Tooltip("Minimum and maximum X offsets from the starting position, in world units.")]
        [SerializeField] private Vector2 horizontalLimits = new Vector2(-6f, 6f);
        [Tooltip("Minimum and maximum Y offsets from the starting position, in world units.")]
        [SerializeField] private Vector2 verticalLimits = new Vector2(-2f, 4f);

        private Vector3 startPosition;

        private void Awake()
        {
            startPosition = transform.position;
            if (inputSource == null)
            {
                Debug.LogError("Assign a FlightInputSource to PlayerFlight before playing.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector2 steering = inputSource != null && inputSource.isActiveAndEnabled
                ? inputSource.ReadSteering() : Vector2.zero;
            transform.position = FlightMovement.Step(transform.position, steering, Time.deltaTime,
                forwardSpeed, steeringSpeed, GetBounds(startPosition));
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

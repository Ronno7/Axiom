using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Third-person chase camera for arena flight. It sits behind and above the ship's direction of
    /// travel and springs after it in position (clamped, so a fast ship never leaves the frame). It looks a little
    /// ahead of the ship and widens the field of view above cruise. In arcade flight it follows only part of the
    /// bank, so the horizon stays readable, and doesn't lag in rotation. In full rotation it turns with the ship
    /// (through loops and rolls, as in a flight game), easing after it briefly.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private ShipFlight target;
        [SerializeField, Min(1f)] private float distance = 7.5f;
        [SerializeField] private float height = 2f;
        [Tooltip("Seconds for the position spring to catch up (smooth time).")]
        [SerializeField, Min(0f)] private float positionLag = 0.15f;
        [Tooltip("Furthest the camera may trail its ideal spot, metres.")]
        [SerializeField, Min(0f)] private float maxLag = 2f;
        [SerializeField, Min(0f)] private float lookAhead = 20f;
        [Tooltip("Share of the ship's bank the camera rolls with. 0 keeps the horizon level.")]
        [SerializeField, Range(0f, 1f)] private float rollFollow = 0.25f;
        [Tooltip("Full rotation only: seconds for the camera's turn to catch up with the ship's.")]
        [SerializeField, Min(0f)] private float rotationLag = 0.06f;
        [SerializeField, Range(30f, 100f)] private float baseFieldOfView = 62f;
        [Tooltip("Extra field of view at full throttle.")]
        [SerializeField, Range(0f, 30f)] private float speedFieldOfView = 10f;

        private Camera view;
        private Vector3 springVelocity;
        private bool placed;
        private int resetFrame = -1;

        private void Awake()
        {
            view = GetComponent<Camera>();
            if (target == null)
            {
                Debug.LogError("Assign a ShipFlight target to ChaseCamera before playing.", this);
                enabled = false;
            }
        }

        /// <summary>The settled chase pose for the current actor state, without moving the camera.
        /// Includes the spring's steady trailing distance at the ship's current velocity, so a camera
        /// placed here and handed over with ResetToTarget keeps still instead of dropping back.</summary>
        public void GetTargetPose(out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            ArenaFlightState s = target.State;
            position = s.Position + s.Heading * new Vector3(0f, height, -distance)
                + SteadyTrail(target.Velocity, positionLag, maxLag);
            rotation = RotationAt(s, position);
            fieldOfView = FieldOfViewAt(s.Speed);
        }

        /// <summary>Where SmoothDamp settles behind a target moving at constant velocity: about
        /// velocity × smoothing time behind it, never more than the maximum lag.</summary>
        public static Vector3 SteadyTrail(Vector3 velocity, float smoothTime, float maxTrail) =>
            Vector3.ClampMagnitude(-velocity * smoothTime, maxTrail);

        /// <summary>Hands the camera back from entry already moving with the ship, so the spring
        /// continues from its steady state.</summary>
        public void ResetToTarget()
        {
            if (view == null) view = GetComponent<Camera>();
            GetTargetPose(out Vector3 position, out Quaternion rotation, out float fieldOfView);
            transform.SetPositionAndRotation(position, rotation);
            view.fieldOfView = fieldOfView;
            springVelocity = target.Velocity;
            placed = true;
            // The ship hasn't moved yet this frame; integrating now would nudge the camera ahead of it.
            resetFrame = Time.frameCount;
        }

        private Quaternion RotationAt(ArenaFlightState s, Vector3 position)
        {
            Quaternion heading = s.Heading;
            Vector3 look = s.Position + heading * (Vector3.forward * lookAhead) - position;
            Quaternion rotation = Quaternion.LookRotation(look, heading * Vector3.up);
            return s.FullRotation ? rotation : rotation * Quaternion.Euler(0f, 0f, -s.Bank * rollFollow);
        }

        private float FieldOfViewAt(float speed)
        {
            float boost = Mathf.InverseLerp(target.CruiseSpeed, target.MaxSpeed, speed);
            return baseFieldOfView + speedFieldOfView * boost;
        }

        private void LateUpdate()
        {
            ArenaFlightState s = target.State;
            Quaternion heading = s.Heading;
            Vector3 ideal = s.Position + heading * new Vector3(0f, height, -distance);
            if (!placed || Time.deltaTime <= 0f || Time.frameCount == resetFrame)
            {
                if (!placed) transform.position = ideal;
                placed = true;
            }
            else
            {
                Vector3 p = Vector3.SmoothDamp(transform.position, ideal, ref springVelocity, positionLag);
                Vector3 trail = p - ideal;
                transform.position = trail.magnitude > maxLag ? ideal + trail.normalized * maxLag : p;
            }

            Quaternion goal = RotationAt(s, transform.position);
            if (!s.FullRotation)
            {
                transform.rotation = goal;
            }
            else
            {
                float share = Time.deltaTime > 0f && rotationLag > 0f
                    ? 1f - Mathf.Exp(-Time.deltaTime / rotationLag) : 1f;
                transform.rotation = Quaternion.Slerp(transform.rotation, goal, share);
            }

            view.fieldOfView = FieldOfViewAt(s.Speed);
        }
    }
}

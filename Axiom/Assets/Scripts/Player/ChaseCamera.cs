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
        [SerializeField] private ArenaFlight target;
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

        private void Awake()
        {
            view = GetComponent<Camera>();
            if (target == null)
            {
                Debug.LogError("Assign an ArenaFlight target to ChaseCamera before playing.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            ArenaFlightState s = target.State;
            Quaternion heading = s.Heading;
            Vector3 ideal = s.Position + heading * new Vector3(0f, height, -distance);
            if (!placed || Time.deltaTime <= 0f)
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

            Vector3 look = s.Position + heading * (Vector3.forward * lookAhead) - transform.position;
            if (!s.FullRotation)
            {
                transform.rotation = Quaternion.LookRotation(look, heading * Vector3.up)
                    * Quaternion.Euler(0f, 0f, -s.Bank * rollFollow);
            }
            else
            {
                Quaternion goal = Quaternion.LookRotation(look, heading * Vector3.up);
                float share = Time.deltaTime > 0f && rotationLag > 0f
                    ? 1f - Mathf.Exp(-Time.deltaTime / rotationLag) : 1f;
                transform.rotation = Quaternion.Slerp(transform.rotation, goal, share);
            }

            float boost = Mathf.InverseLerp(target.CruiseSpeed, target.MaxSpeed, s.Speed);
            view.fieldOfView = baseFieldOfView + speedFieldOfView * boost;
        }
    }
}

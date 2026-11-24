using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Third-person chase camera. It sits behind and above the ship and springs after it in position
    /// (clamped, so a fast ship never leaves the frame). It looks a little ahead of the ship, turns with it through
    /// loops and rolls, easing after it briefly, and widens the field of view above cruise. In the arena, free look
    /// swings the view around the ship and back behind it when let go; it never turns the ship.</summary>
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
        [Tooltip("Seconds for the camera's turn to catch up with the ship's.")]
        [SerializeField, Min(0f)] private float rotationLag = 0.06f;
        [SerializeField, Range(30f, 100f)] private float baseFieldOfView = 62f;
        [Tooltip("Extra field of view at full throttle.")]
        [SerializeField, Range(0f, 30f)] private float speedFieldOfView = 10f;

        [Header("Free look (arena only)")]
        [Tooltip("How far free look turns the view left or right of the nose, degrees.")]
        [SerializeField, Range(0f, 180f)] private float lookAcross = 150f;
        [Tooltip("How far it turns the view up or down, degrees.")]
        [SerializeField, Range(0f, 85f)] private float lookUpDown = 60f;
        [Tooltip("Seconds for the view to follow the stick, and to swing back behind the ship when it's let go.")]
        [SerializeField, Min(0f)] private float lookLag = 0.12f;

        /// <summary>The camera stays exactly where it is: it holds on a crash, and still over a rebuild.</summary>
        public bool Held { get; set; }

        private Camera view;
        private Vector3 springVelocity;
        /// <summary>The chase spring's own position, behind the ship, before free look turns it around the ship.</summary>
        private Vector3 chase;
        /// <summary>Free look: degrees right (x) and up (y) of straight ahead.</summary>
        private Vector2 look, lookVelocity;
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
            position = s.Position + s.Attitude * new Vector3(0f, height, -distance)
                + SteadyTrail(target.Velocity, positionLag, maxLag);
            rotation = RotationAt(s, position);
            fieldOfView = FieldOfViewAt(s.Speed);
        }

        /// <summary>Where SmoothDamp settles behind a target moving at constant velocity: about
        /// velocity × smoothing time behind it, never more than the maximum lag.</summary>
        public static Vector3 SteadyTrail(Vector3 velocity, float smoothTime, float maxTrail) =>
            Vector3.ClampMagnitude(-velocity * smoothTime, maxTrail);

        /// <summary>Hands the camera back from entry already moving with the ship, so the spring
        /// continues from its steady state. Free look starts again from straight ahead.</summary>
        public void ResetToTarget()
        {
            if (view == null) view = GetComponent<Camera>();
            GetTargetPose(out Vector3 position, out Quaternion rotation, out float fieldOfView);
            transform.SetPositionAndRotation(position, rotation);
            view.fieldOfView = fieldOfView;
            chase = position;
            springVelocity = target.Velocity;
            look = lookVelocity = Vector2.zero;
            placed = true;
            // The ship hasn't moved yet this frame; integrating now would nudge the camera ahead of it.
            resetFrame = Time.frameCount;
        }

        private Quaternion RotationAt(ArenaFlightState s, Vector3 position)
        {
            Vector3 ahead = s.Position + s.Attitude * (Vector3.forward * lookAhead) - position;
            return Quaternion.LookRotation(ahead, s.Attitude * Vector3.up);
        }

        /// <summary>Free look as a turn about the ship: <paramref name="angles"/> degrees right (x) and up (y) in the
        /// ship's own frame, whichever way up it is.</summary>
        public static Quaternion Orbit(Quaternion ship, Vector2 angles) =>
            ship * Quaternion.Euler(-angles.y, angles.x, 0f) * Quaternion.Inverse(ship);

        /// <summary>The view follows the stick in the arena. On the rail, or with the stick let go, it swings back
        /// behind the ship.</summary>
        private void FollowLook()
        {
            Vector2 stick = target.enabled && target.RailReleased ? target.CurrentInput.Look : Vector2.zero;
            var aim = new Vector2(stick.x * lookAcross, stick.y * lookUpDown);
            look = Vector2.SmoothDamp(look, aim, ref lookVelocity, lookLag, Mathf.Infinity, Time.deltaTime);
        }

        private float FieldOfViewAt(float speed)
        {
            float boost = Mathf.InverseLerp(target.CruiseSpeed, target.MaxSpeed, speed);
            return baseFieldOfView + speedFieldOfView * boost;
        }

        private void LateUpdate()
        {
            if (Held) return;
            ArenaFlightState s = target.State;
            Vector3 ideal = s.Position + s.Attitude * new Vector3(0f, height, -distance);
            if (!placed || Time.deltaTime <= 0f || Time.frameCount == resetFrame)
            {
                if (!placed) chase = ideal;
                placed = true;
            }
            else
            {
                Vector3 p = Vector3.SmoothDamp(chase, ideal, ref springVelocity, positionLag);
                Vector3 trail = p - ideal;
                chase = trail.magnitude > maxLag ? ideal + trail.normalized * maxLag : p;
            }

            FollowLook();
            Quaternion orbit = Orbit(s.Attitude, look);
            transform.position = s.Position + orbit * (chase - s.Position);
            Quaternion goal = orbit * RotationAt(s, chase);
            float share = Time.deltaTime > 0f && rotationLag > 0f ? 1f - Mathf.Exp(-Time.deltaTime / rotationLag) : 1f;
            transform.rotation = Quaternion.Slerp(transform.rotation, goal, share);

            view.fieldOfView = FieldOfViewAt(s.Speed);
        }
    }
}

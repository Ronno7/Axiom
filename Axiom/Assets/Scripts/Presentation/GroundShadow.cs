using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Drop shadow directly below the caster on the ground (the lowest surface beneath it),
    /// so height and lateral position read at a glance. It passes under obstacles instead of climbing
    /// onto them, and they hide it naturally. The project shader receives no realtime shadows, so this
    /// flattened silhouette stands in for one. It shrinks slightly with height as an altitude cue.</summary>
    [DisallowMultipleComponent]
    public sealed class GroundShadow : MonoBehaviour
    {
        [SerializeField] private Transform caster;
        [Tooltip("Colliders considered for the ground. Exclude the Player layer.")]
        [SerializeField] private LayerMask surfaces = ~0;
        [SerializeField, Min(0.1f)] private float maxDistance = 30f;
        [Tooltip("Lift above the surface to avoid z-fighting with floor markings.")]
        [SerializeField, Min(0f)] private float surfaceOffset = 0.05f;
        [Tooltip("Scale multiplier at height 0 (x) and at Shrink Height or above (y).")]
        [SerializeField] private Vector2 scaleRange = new Vector2(1f, 0.7f);
        [SerializeField, Min(0.1f)] private float shrinkHeight = 8f;

        private readonly RaycastHit[] hits = new RaycastHit[8];
        private Renderer[] renderers;
        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<Renderer>();
        }

        private void LateUpdate()
        {
            int count = caster != null ? Physics.RaycastNonAlloc(caster.position, Vector3.down, hits,
                maxDistance, surfaces, QueryTriggerInteraction.Ignore) : 0;
            // The farthest hit is the ground; nearer hits are obstacles the ship is passing over.
            int ground = -1;
            for (int i = 0; i < count; i++)
                if (ground < 0 || hits[i].distance > hits[ground].distance) ground = i;

            foreach (Renderer r in renderers) r.enabled = ground >= 0;
            if (ground < 0) return;

            RaycastHit hit = hits[ground];
            Vector3 forward = Vector3.ProjectOnPlane(caster.forward, hit.normal);
            transform.SetPositionAndRotation(hit.point + hit.normal * surfaceOffset,
                Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward, hit.normal));
            float t = Mathf.Clamp01(hit.distance / shrinkHeight);
            transform.localScale = baseScale * Mathf.Lerp(scaleRange.x, scaleRange.y, t);
        }
    }
}

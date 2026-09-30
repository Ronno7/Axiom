using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>A stretch of course that calls for a throttle setting, from this transform's Z
    /// forward by Length. The HUD shows the recommended band while the ship is inside; floor cues
    /// (children of this object) mark it in the world, so moving the zone moves its cues.</summary>
    [DisallowMultipleComponent]
    public sealed class ThrottleZone : MonoBehaviour
    {
        public enum Kind { Slow, Fast }

        [SerializeField] private Kind kind = Kind.Slow;
        [SerializeField, Min(1f)] private float length = 50f;
        [Tooltip("Recommended speed as multiples of cruise (min, max).")]
        [SerializeField] private Vector2 recommendedCruiseMultiple = new Vector2(0.6f, 0.8f);

        public Kind Type => kind;
        public Vector2 RecommendedCruiseMultiple => recommendedCruiseMultiple;

        public bool Contains(float z) => z >= transform.position.z && z <= transform.position.z + length;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == Kind.Slow ? Color.cyan : Color.yellow;
            Vector3 start = transform.position;
            Gizmos.DrawWireCube(start + Vector3.forward * (length * 0.5f), new Vector3(12f, 6f, length));
        }
    }
}

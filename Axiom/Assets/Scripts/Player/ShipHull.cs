using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Detects the ship overlapping scenery or targets and damages the shield. The ship
    /// flies on through; ShipShield's protection window stops one obstacle from hitting twice.
    /// The BoxCollider (on the Player layer) is also what enemy shots hit.</summary>
    [DefaultExecutionOrder(10)] // After PlayerFlight has moved the ship this frame.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(ShipShield))]
    public sealed class ShipHull : MonoBehaviour
    {
        [Tooltip("What counts as an obstacle. Exclude the Player layer.")]
        [SerializeField] private LayerMask obstacleLayers = ~0;

        private readonly Collider[] overlaps = new Collider[4];
        private BoxCollider hull;
        private ShipShield shield;

        private void Awake()
        {
            hull = GetComponent<BoxCollider>();
            shield = GetComponent<ShipShield>();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            Vector3 center = transform.TransformPoint(hull.center);
            Vector3 halfExtents = Vector3.Scale(hull.size, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlaps, transform.rotation,
                obstacleLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i] == hull) continue;
                shield.ApplyHit();
                return;
            }
        }
    }
}

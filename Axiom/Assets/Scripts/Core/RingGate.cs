using System;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// A ring the ship can fly through in any direction (arena rings, unlike the rail's +Z hoops). Its opening is data:
    /// a circle of <see cref="Radius"/> metres around this transform, in the plane facing its forward axis. A pass is a
    /// forward crossing of that plane inside the circle. Gameplay-free on its own; listeners (ring flash, a lesson)
    /// decide what a pass means.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RingGate : MonoBehaviour
    {
        /// <summary>Any ring passed this frame.</summary>
        public static event Action<RingGate> Passed;

        [SerializeField] private ShipFlight ship;
        [Tooltip("Opening radius in metres, independent of the visual's scale.")]
        [SerializeField, Min(0.5f)] private float radius = 10f;

        private Vector3 lastShip;
        private bool havePrevious;

        public float Radius => radius;
        public bool WasPassed { get; private set; }

        public void ResetPass() => WasPassed = false;

        /// <summary>Whether travel from <paramref name="from"/> to <paramref name="to"/> crosses the ring's plane
        /// forwards (along <paramref name="normal"/>) inside its opening.</summary>
        public static bool Crosses(Vector3 center, Vector3 normal, float radius, Vector3 from, Vector3 to)
        {
            float a = Vector3.Dot(from - center, normal), b = Vector3.Dot(to - center, normal);
            if (a >= 0f || b < 0f) return false;
            Vector3 at = Vector3.Lerp(from, to, a / (a - b));
            return Vector3.ProjectOnPlane(at - center, normal).sqrMagnitude <= radius * radius;
        }

        private void Update()
        {
            if (ship == null || !ship.isActiveAndEnabled) { havePrevious = false; return; }
            Vector3 now = ship.transform.position;
            if (havePrevious && Crosses(transform.position, transform.forward, radius, lastShip, now))
            {
                WasPassed = true;
                Passed?.Invoke(this);
            }
            lastShip = now;
            havePrevious = true;
        }
    }
}

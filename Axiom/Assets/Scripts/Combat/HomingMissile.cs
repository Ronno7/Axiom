using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>A pooled, finite-life missile. Sweeps against scenery as well as targets; never retargets.</summary>
    [DisallowMultipleComponent]
    public sealed class HomingMissile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float radius = 0.3f;
        [SerializeField] private TrailRenderer trail;
        [SerializeField, Range(1f, 180f)] private float seekerCone = 65f;
        private ShootableTarget target;
        private float speed, turnRate, remaining;
        private int damage;
        private LayerMask layers;

        public void Launch(Vector3 position, Vector3 direction, ShootableTarget destination, float flightSpeed,
            float degreesPerSecond, float lifetime, int hitDamage, LayerMask hitLayers)
        {
            target = destination;
            speed = flightSpeed;
            turnRate = degreesPerSecond;
            remaining = lifetime;
            damage = hitDamage;
            layers = hitLayers;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            gameObject.SetActive(true);
            if (trail != null) trail.Clear();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, remaining);
            if (dt <= 0f) return;
            Vector3 from = transform.position;
            if (target != null && target.isActiveAndEnabled && !target.IsDestroyed)
            {
                Vector3 offset = target.transform.position - from;
                // An overshoot is a miss: do not circle back or acquire a different target.
                if (Vector3.Angle(transform.forward, offset) > seekerCone) target = null;
                else if (offset.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(offset), turnRate * dt);
            }
            Vector3 to = from + transform.forward * speed * dt;
            if (WeaponRules.TrySweep(from, to, radius, layers, out RaycastHit hit))
            {
                hit.collider.GetComponentInParent<IDamageable>()?.ApplyHit(damage);
                gameObject.SetActive(false);
                return;
            }
            transform.position = to;
            remaining -= dt;
            if (remaining <= 0f) gameObject.SetActive(false);
        }
    }
}

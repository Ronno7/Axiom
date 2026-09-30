using System;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Pooled straight-line shot. Any collider stops it; an IDamageable also takes a hit.
    /// The owning weapon reactivates it; it deactivates itself on impact or when its lifetime ends.</summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [Tooltip("Collision radius for the swept hit test, in world units.")]
        [SerializeField, Min(0.01f)] private float radius = 0.15f;

        private Vector3 velocity;
        private float remainingLifetime;
        private LayerMask hitLayers;
        private Action onDamageDealt;

        /// <param name="damageDealt">Optional; invoked when this shot damages something.</param>
        public void Launch(Vector3 position, Vector3 worldVelocity, float lifetime, LayerMask layers,
            Action damageDealt = null)
        {
            velocity = worldVelocity;
            remainingLifetime = lifetime;
            hitLayers = layers;
            onDamageDealt = damageDealt;
            transform.SetPositionAndRotation(position,
                velocity.sqrMagnitude > 0f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            Vector3 from = transform.position;
            Vector3 to = from + velocity * deltaTime;
            if (WeaponRules.TrySweep(from, to, radius, hitLayers, out RaycastHit hit))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.ApplyHit()) onDamageDealt?.Invoke();
                gameObject.SetActive(false);
                return;
            }

            transform.position = to;
            remainingLifetime -= deltaTime;
            if (remainingLifetime <= 0f) gameObject.SetActive(false);
        }
    }
}

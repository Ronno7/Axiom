using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Simple enemy attack: while the player approaches within range, periodically fires a
    /// slow shot at the player's current position. Dodgeable by steering. Stops when this object
    /// is deactivated (for example when its ShootableTarget breaks).</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTurret : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform player;
        [Tooltip("Fires while the player is this many units behind the turret along Z (min, max).")]
        [SerializeField] private Vector2 engagementRange = new Vector2(12f, 45f);
        [SerializeField, Min(0.2f)] private float fireInterval = 1.6f;
        [SerializeField, Min(1f)] private float projectileSpeed = 16f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
        [Tooltip("Spawn distance from the center, outside this object's own collider.")]
        [SerializeField, Min(0f)] private float spawnOffset = 1.6f;
        [Tooltip("Include the Player layer.")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField, Min(1)] private int poolSize = 4;

        private ProjectilePool pool;
        private float cooldown;

        private void Awake()
        {
            if (projectilePrefab == null || player == null)
            {
                Debug.LogError("Assign a projectile prefab and player to EnemyTurret.", this);
                enabled = false;
                return;
            }
            pool = new ProjectilePool(projectilePrefab, poolSize, name + " Shots");
            cooldown = fireInterval * 0.5f;
        }

        private void OnDestroy() => pool?.Dispose();

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            float ahead = transform.position.z - player.position.z;
            if (ahead < engagementRange.x || ahead > engagementRange.y) return;

            cooldown -= deltaTime;
            if (cooldown > 0f) return;
            cooldown += fireInterval;

            Projectile projectile = pool.Take();
            if (projectile == null) return;
            Vector3 direction = (player.position - transform.position).normalized;
            projectile.Launch(transform.position + direction * spawnOffset, direction * projectileSpeed,
                projectileLifetime, hitLayers);
        }
    }
}

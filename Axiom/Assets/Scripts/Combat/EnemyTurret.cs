using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Simple enemy attack: while the player approaches within range, periodically fires a
    /// slow shot at the player's current position (or where the player is heading, with Lead). Dodgeable by
    /// steering. On the rail, range is measured along Z; in an arena, as straight distance. Stops the moment its
    /// ShootableTarget (if any) breaks, not when the shatter effect later deactivates the object, and
    /// when this object is deactivated.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTurret : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform player;
        [Tooltip("Fires while the player is this many units behind the turret along Z (min, max), or this far "
               + "away in any direction with Measure Distance on.")]
        [SerializeField] private Vector2 engagementRange = new Vector2(12f, 45f);
        [Tooltip("Arena turrets: measure straight distance to the player instead of distance along the course.")]
        [SerializeField] private bool measureDistance;
        [Tooltip("0 aims at the player; 1 aims where the player will be when the shot arrives.")]
        [SerializeField, Range(0f, 1f)] private float lead;
        [SerializeField, Min(0.2f)] private float fireInterval = 1.6f;
        [SerializeField, Min(1f)] private float projectileSpeed = 16f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
        [Tooltip("Spawn distance from the center, outside this object's own collider.")]
        [SerializeField, Min(0f)] private float spawnOffset = 1.6f;
        [Tooltip("Include the Player layer.")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField, Min(1)] private int poolSize = 4;

        private ProjectilePool pool;
        private ShootableTarget target;
        private float cooldown;
        private Vector3 lastPlayerPosition;
        private Vector3 playerVelocity;

        private void Awake()
        {
            target = GetComponent<ShootableTarget>();
            if (target != null) target.Destroyed += OnTargetDestroyed;
            if (projectilePrefab == null || player == null)
            {
                Debug.LogError("Assign a projectile prefab and player to EnemyTurret.", this);
                enabled = false;
                return;
            }
            pool = new ProjectilePool(projectilePrefab, poolSize, name + " Shots");
            cooldown = fireInterval * 0.5f;
            lastPlayerPosition = player.position;
        }

        private void OnDestroy()
        {
            if (target != null) target.Destroyed -= OnTargetDestroyed;
            pool?.Dispose();
        }

        private void OnTargetDestroyed(ShootableTarget _) => enabled = false;

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            playerVelocity = (player.position - lastPlayerPosition) / deltaTime;
            lastPlayerPosition = player.position;
            float ahead = measureDistance
                ? Vector3.Distance(transform.position, player.position)
                : transform.position.z - player.position.z;
            if (ahead < engagementRange.x || ahead > engagementRange.y) return;

            cooldown -= deltaTime;
            if (cooldown > 0f) return;
            cooldown += fireInterval;

            Projectile projectile = pool.Take();
            if (projectile == null) return;
            Vector3 aim = player.position;
            if (lead > 0f)
                aim += playerVelocity * (lead * Vector3.Distance(transform.position, aim) / projectileSpeed);
            Vector3 direction = (aim - transform.position).normalized;
            projectile.Launch(transform.position + direction * spawnOffset, direction * projectileSpeed,
                projectileLifetime, hitLayers);
        }
    }
}

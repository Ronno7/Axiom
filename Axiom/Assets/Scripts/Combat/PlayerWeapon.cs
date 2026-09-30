using System;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Hold-to-fire weapon. Reads fire intent from PlayerFlight's per-frame input snapshot
    /// and alternates muzzles, aiming every shot at the reticle. Projectiles come from a fixed pool
    /// created at startup, so shots never accumulate.</summary>
    [DefaultExecutionOrder(10)] // After PlayerFlight, so input and muzzle positions are current.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerFlight))]
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform[] muzzles;
        [Tooltip("Shots converge here. Keep the visible reticle on this transform.")]
        [SerializeField] private Transform aimPoint;
        [Tooltip("Seconds between shots while fire is held.")]
        [SerializeField, Min(0.02f)] private float shotInterval = 0.125f;
        [Tooltip("Projectile speed relative to the ship, in world units per second.")]
        [SerializeField, Min(1f)] private float projectileSpeed = 70f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 1.5f;
        [Tooltip("Exclude the player's own layer.")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [Tooltip("Maximum live projectiles. Shots are skipped if all are in flight.")]
        [SerializeField, Min(1)] private int poolSize = 24;

        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }

        private ProjectilePool pool;
        private PlayerFlight flight;
        private Action countHit;
        private float cooldown;
        private int nextMuzzle;

        private void Awake()
        {
            flight = GetComponent<PlayerFlight>();
            countHit = () => Hits++;
            if (projectilePrefab == null || aimPoint == null || muzzles == null || muzzles.Length == 0)
            {
                Debug.LogError("Assign a projectile prefab, aim point, and at least one muzzle to PlayerWeapon.", this);
                enabled = false;
                return;
            }
            pool = new ProjectilePool(projectilePrefab, poolSize, "PlayerProjectiles");
        }

        private void OnDestroy() => pool?.Dispose();

        private void Update()
        {
            bool fireHeld = flight.isActiveAndEnabled && flight.CurrentInput.FireHeld;
            int shots = WeaponRules.ShotsThisFrame(ref cooldown, fireHeld, Time.deltaTime, shotInterval);
            for (int i = 0; i < shots; i++) Fire();
        }

        private void Fire()
        {
            Projectile projectile = pool.Take();
            if (projectile == null) return;

            Transform muzzle = muzzles[nextMuzzle];
            nextMuzzle = (nextMuzzle + 1) % muzzles.Length;
            Vector3 velocity = WeaponRules.ShotVelocity(muzzle.position, aimPoint.position,
                projectileSpeed, flight.CurrentSpeed);
            projectile.Launch(muzzle.position, velocity, projectileLifetime, hitLayers, countHit);
            ShotsFired++;
        }
    }
}

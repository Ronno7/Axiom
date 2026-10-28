using System;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Hold-to-fire weapon. Reads fire intent from the ship's per-frame input snapshot
    /// and alternates muzzles, aiming every shot at the reticle. Optional aim assist sends a shot through
    /// the target nearest the firing line. Projectiles come from a fixed pool created at startup, so shots
    /// never accumulate.</summary>
    [DefaultExecutionOrder(10)] // After the flight component, so input and muzzle positions are current.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipFlight))]
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
        [Tooltip("Aim assist: shots go through the target nearest the firing line within this many degrees "
               + "of it. 0 = off.")]
        [SerializeField, Range(0f, 15f)] private float aimAssistCone = 0f;
        [SerializeField, Min(1f)] private float aimAssistRange = 120f;

        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        /// <summary>The target aim assist will send shots through this frame, or null (assist off, or nothing
        /// near the firing line). Presentation shows it so the player knows what a shot will hit.</summary>
        public ShootableTarget AssistTarget { get; private set; }

        private ProjectilePool pool;
        private ShipFlight flight;
        private Vector3[] assistPoints = new Vector3[16];
        private Action countHit;
        private float cooldown;
        private int nextMuzzle;

        private void Awake()
        {
            flight = GetComponent<ShipFlight>();
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
            AssistTarget = PickAssistTarget();
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
            Vector3 velocity = AssistTarget != null
                ? WeaponRules.ShotVelocityThrough(muzzle.position, AssistTarget.transform.position, projectileSpeed,
                    flight.Velocity)
                : WeaponRules.ShotVelocity(muzzle.position, aimPoint.position, projectileSpeed, flight.Velocity);
            projectile.Launch(muzzle.position, velocity, projectileLifetime, hitLayers, countHit);
            ShotsFired++;
        }

        private ShootableTarget PickAssistTarget()
        {
            if (aimAssistCone <= 0f || !enabled) return null;
            var targets = ShootableTarget.Active;
            if (assistPoints.Length < targets.Count) assistPoints = new Vector3[targets.Count * 2];
            for (int i = 0; i < targets.Count; i++) assistPoints[i] = targets[i].transform.position;
            Vector3 origin = transform.position;
            int pick = WeaponRules.PickAssistTarget(origin, aimPoint.position - origin, assistPoints, targets.Count,
                aimAssistCone, aimAssistRange);
            return pick < 0 ? null : targets[pick];
        }
    }
}

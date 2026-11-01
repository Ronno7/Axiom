using System.Collections.Generic;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Arena-only missile launcher. Flight owns input, MissileLockState owns the bank and locks.</summary>
    [DefaultExecutionOrder(5)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerWeapon))]
    public sealed class LockOnWeapon : MonoBehaviour
    {
        [SerializeField] private HomingMissile missilePrefab;
        [SerializeField, Range(1, 8)] private int bankSize = 4;
        [SerializeField, Min(0.01f)] private float firstLockTime = 0.45f;
        [SerializeField, Min(0.01f)] private float additionalLockTime = 0.3f;
        [SerializeField, Min(0f)] private float retentionTime = 1.5f;
        [SerializeField, Min(0.01f)] private float refillTime = 5f;
        [SerializeField, Range(1f, 45f)] private float acquisitionCone = 12f;
        [SerializeField, Min(0f)] private float minimumRange = 90f;
        [SerializeField, Min(1f)] private float range = 650f;
        [SerializeField, Min(1f)] private float missileSpeed = 190f;
        [SerializeField, Min(1f)] private float turnRate = 45f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField, Min(1)] private int damage = 3;
        private readonly Dictionary<int, ShootableTarget> targets = new Dictionary<int, ShootableTarget>();
        private readonly HashSet<int> valid = new HashSet<int>();
        private readonly HashSet<int> inCone = new HashSet<int>();
        private ShipFlight flight;
        private PlayerWeapon gun;
        private Transform poolRoot;
        private HomingMissile[] pool;
        private int nextMuzzle;

        public MissileLockState State { get; private set; }
        public int MissilesFired { get; private set; }
        public bool CloseTargetInCone { get; private set; }
        public float MinimumRange => minimumRange;
        public float MaximumRange => range;
        public bool SuppressGun => flight != null && flight.CurrentInput.LockHeld;
        public bool Available => flight != null && flight.isActiveAndEnabled && !flight.InputLocked;

        private void Awake()
        {
            flight = GetComponent<ShipFlight>();
            gun = GetComponent<PlayerWeapon>();
            State = new MissileLockState(bankSize, firstLockTime, additionalLockTime, retentionTime, refillTime);
            if (missilePrefab == null)
            {
                Debug.LogError("Assign a missile prefab to LockOnWeapon.", this);
                enabled = false;
                return;
            }
            poolRoot = new GameObject("PlayerMissiles").transform;
            // Enough for the full bank plus every missile that can regenerate during a missile's life.
            pool = new HomingMissile[bankSize + Mathf.CeilToInt(lifetime / Mathf.Max(0.01f, refillTime)) + 1];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(missilePrefab, poolRoot);
                pool[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (flight.CurrentInput.LockCancelled) State.Cancel();
            valid.Clear();
            inCone.Clear();
            targets.Clear();
            int candidate = 0;
            CloseTargetInCone = false;
            float bestAngle = acquisitionCone;
            Vector3 origin = transform.position;
            Vector3 forward = gun.AimPoint.position - origin;
            foreach (ShootableTarget target in ShootableTarget.Active)
            {
                Vector3 offset = target.transform.position - origin;
                if (offset.sqrMagnitude > range * range || !Visible(target, origin)) continue;
                if (offset.sqrMagnitude < minimumRange * minimumRange)
                {
                    if (Vector3.Angle(forward, offset) <= acquisitionCone) CloseTargetInCone = true;
                    continue;
                }
                int id = target.GetInstanceID();
                targets[id] = target;
                valid.Add(id);
                float angle = Vector3.Angle(forward, offset);
                if (angle <= acquisitionCone) inCone.Add(id);
                if (angle <= bestAngle && !ContainsLock(id))
                {
                    bestAngle = angle;
                    candidate = id;
                }
            }
            int[] volley = State.Step(Time.deltaTime, flight.CurrentInput.LockHeld,
                Available && Time.timeScale > 0f, candidate, valid, inCone);
            foreach (int id in volley) Launch(Target(id));
        }

        private bool Visible(ShootableTarget target, Vector3 origin)
        {
            if (!Physics.Linecast(origin, target.transform.position, out RaycastHit hit, gun.HitLayers,
                QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<ShootableTarget>() == target;
        }

        private bool ContainsLock(int id)
        {
            foreach (int locked in State.Locks) if (locked == id) return true;
            return false;
        }

        public ShootableTarget Target(int id) => targets.TryGetValue(id, out ShootableTarget target) ? target : null;

        private void Launch(ShootableTarget target)
        {
            if (target == null) return;
            foreach (HomingMissile missile in pool)
            {
                if (missile.gameObject.activeSelf) continue;
                Transform muzzle = gun.Muzzles[nextMuzzle++ % gun.Muzzles.Length];
                missile.Launch(muzzle.position, transform.forward, target, missileSpeed, turnRate,
                    lifetime, damage, gun.HitLayers);
                MissilesFired++;
                return;
            }
        }

        public void CancelPainting() => State?.Cancel();

        public void ResetBank()
        {
            State?.Reset();
            if (pool != null) foreach (HomingMissile missile in pool) missile.gameObject.SetActive(false);
        }

        private void OnDisable() => CancelPainting();
        private void OnApplicationFocus(bool focus) { if (!focus) CancelPainting(); }
        private void OnDestroy() { if (poolRoot != null) Destroy(poolRoot.gameObject); }
    }
}

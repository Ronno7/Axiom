using System;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>A single destructive pulse per life. Expands from the firing position, through scenery.
    /// Only enemy targets and hostile projectiles are affected; wave recovery does not refill it.</summary>
    [DefaultExecutionOrder(6), DisallowMultipleComponent, RequireComponent(typeof(ShipFlight))]
    public sealed class EmpWeapon : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float radius = 180f;
        [SerializeField, Min(0f)] private float compressionTime = 0.22f;
        [SerializeField, Min(0.1f)] private float expansionTime = 2.8f;
        private ShipFlight flight;
        private float age;
        private ShootableTarget[] victims = new ShootableTarget[32];
        public EmpChargeState State { get; } = new EmpChargeState();
        public event Action Detonated;
        public bool Pulsing { get; private set; }
        public Vector3 Origin { get; private set; }
        public Quaternion Orientation { get; private set; }
        public float Radius => radius;
        public bool Compressing => Pulsing && age < compressionTime;
        public float CompressionProgress => compressionTime > 0f ? Mathf.Clamp01(age / compressionTime) : 1f;
        public float Progress => Mathf.Clamp01((age - compressionTime) / expansionTime);
        public int EnemiesDestroyed { get; private set; }
        public int ShotsCleared { get; private set; }

        private void Awake() => flight = GetComponent<ShipFlight>();

        private void Update()
        {
            bool available = flight.isActiveAndEnabled && !flight.InputLocked;
            if (State.Step(flight.CurrentInput.EmpHeld, available && Time.timeScale > 0f))
            {
                Origin = transform.position;
                Orientation = transform.rotation;
                age = 0f;
                Pulsing = true;
                EnemiesDestroyed = ShotsCleared = 0;
                Detonated?.Invoke();
            }
            if (!available) Pulsing = false;
            if (!Pulsing || Time.deltaTime <= 0f) return;
            age += Time.deltaTime;
            if (Compressing) return;
            float reach = radius * Progress;
            float squared = reach * reach;
            // Destruction modifies the registry and can finish the wave; snapshot before invoking events.
            var active = ShootableTarget.Active;
            if (victims.Length < active.Count) victims = new ShootableTarget[active.Count * 2];
            int count = active.Count;
            for (int i = 0; i < count; i++) victims[i] = active[i];
            for (int i = 0; i < count; i++)
            {
                ShootableTarget target = victims[i];
                if (target != null && !target.IsDestroyed &&
                    (target.transform.position - Origin).sqrMagnitude <= squared &&
                    target.ApplyHit(target.RemainingHitPoints)) EnemiesDestroyed++;
                victims[i] = null;
            }
            foreach (Projectile shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if (shot.Hostile && (shot.transform.position - Origin).sqrMagnitude <= squared)
                {
                    shot.gameObject.SetActive(false);
                    ShotsCleared++;
                }
            if (age >= compressionTime + expansionTime) Pulsing = false;
        }

        public void ResetCharge() { State.Reset(); Pulsing = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) State.Cancel(); }
        private void OnDisable() { State.Cancel(); Pulsing = false; }
    }
}

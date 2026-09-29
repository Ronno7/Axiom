using System;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Takes projectile hits, flashes on each hit, and breaks exactly once. On breaking it
    /// stops colliding, hides its visuals, plays an optional ShatterEffect, then deactivates.</summary>
    [DisallowMultipleComponent]
    public sealed class ShootableTarget : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(1)] private int hitPoints = 3;
        [SerializeField] private Color hitFlashColor = new Color(0.25f, 0.9f, 1f);
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;
        [SerializeField] private ShatterEffect destructionEffect;

        /// <summary>Raised once, when the target breaks. UI and scoring can observe this.</summary>
        public event Action<ShootableTarget> Destroyed;
        public bool IsDestroyed { get; private set; }
        public int RemainingHitPoints { get; private set; }

        private Renderer[] visuals;
        private Collider[] colliders;
        private MaterialPropertyBlock flashBlock;
        private float flashRemaining;

        private void Awake()
        {
            RemainingHitPoints = hitPoints;
            visuals = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor(BaseColorId, hitFlashColor);
        }

        /// <summary>Returns false if the target was already destroyed.</summary>
        public bool ApplyHit(int damage = 1)
        {
            if (IsDestroyed) return false;
            RemainingHitPoints = Mathf.Max(0, RemainingHitPoints - Mathf.Max(1, damage));
            if (RemainingHitPoints == 0) Break();
            else SetFlash(true);
            return true;
        }

        private void Update()
        {
            if (flashRemaining <= 0f) return;
            flashRemaining -= Time.deltaTime;
            if (flashRemaining <= 0f) SetFlash(false);
        }

        private void SetFlash(bool on)
        {
            flashRemaining = on ? hitFlashDuration : 0f;
            foreach (Renderer visual in visuals)
                visual.SetPropertyBlock(on ? flashBlock : null);
        }

        private void Break()
        {
            IsDestroyed = true;
            SetFlash(false);
            foreach (Collider c in colliders) c.enabled = false;
            foreach (Renderer visual in visuals) visual.enabled = false;
            Destroyed?.Invoke(this);

            if (destructionEffect != null) destructionEffect.Play(() => gameObject.SetActive(false));
            else gameObject.SetActive(false);
        }
    }
}

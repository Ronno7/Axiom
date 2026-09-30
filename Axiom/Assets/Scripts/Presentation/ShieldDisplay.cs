using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Diegetic shield readout: hull segments switch from lit to dark as the shield is lost,
    /// and the aircraft blinks while post-hit protection lasts.</summary>
    [DisallowMultipleComponent]
    public sealed class ShieldDisplay : MonoBehaviour
    {
        [SerializeField] private ShipShield shield;
        [Tooltip("One renderer per shield segment, lost from last to first.")]
        [SerializeField] private Renderer[] segments;
        [SerializeField] private Material litMaterial;
        [SerializeField] private Material lostMaterial;
        [Tooltip("Renderers hidden and shown while protected. Usually the whole aircraft.")]
        [SerializeField] private Renderer[] blinkRenderers;
        [SerializeField, Min(1f)] private float blinkRate = 12f;

        private bool blinking;

        private void OnEnable()
        {
            if (shield == null) return;
            shield.Changed += ShowSegments;
            if (shield.State != null) ShowSegments(shield.State.Current);
        }

        private void OnDisable()
        {
            if (shield != null) shield.Changed -= ShowSegments;
            SetVisible(true);
        }

        private void ShowSegments(int remaining)
        {
            for (int i = 0; i < segments.Length; i++)
                segments[i].sharedMaterial = i < remaining ? litMaterial : lostMaterial;
        }

        private void Update()
        {
            bool protectedNow = shield != null && shield.State.IsProtected;
            if (!protectedNow && !blinking) return;
            blinking = protectedNow;
            // Unscaled time keeps the blink phase steady; while paused, the ship stays visible.
            SetVisible(!protectedNow || Time.timeScale == 0f
                || Mathf.Repeat(Time.unscaledTime * blinkRate, 1f) < 0.5f);
        }

        private void SetVisible(bool visible)
        {
            if (blinkRenderers == null) return;
            foreach (Renderer r in blinkRenderers) r.enabled = visible;
        }
    }
}

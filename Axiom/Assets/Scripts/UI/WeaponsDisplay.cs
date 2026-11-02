using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Owns the weapons panel and target markers on the HUD, and the aim diamonds, which dim while SAFE and flare
    /// on arming. Reads the weapons; never changes them.</summary>
    [DefaultExecutionOrder(110)] // After the chase camera and MenuLook's panel routing.
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class WeaponsDisplay : MonoBehaviour
    {
        private const float FlareSeconds = 0.5f, RefusedSeconds = 0.5f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private LockOnWeapon weapon;
        [Tooltip("The aim diamonds, which dim while SAFE.")]
        [SerializeField] private Renderer[] reticles = new Renderer[0];

        private UIDocument document;
        private WeaponsPanel panel;
        private TargetMarkers markers;
        private PlayerWeapon gun;
        private MasterArmInput authority;
        private LevelFlow flow;
        private Camera view;
        private MaterialPropertyBlock tint;
        private float flareUntil, refusedUntil;
        private bool armKnown, wasArmed, tinted;

        /// <summary>Something needs weapons now: while SAFE, the X, the word and the bar pulse instead of sitting quiet.</summary>
        public bool CallForArming { get; set; }

        /// <summary>0–1 while an entry draws the instrument in. Shows no target markers until 1.</summary>
        public float Assembly { get; set; } = 1f;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            view = Camera.main;
            gun = weapon.GetComponent<PlayerWeapon>();
            authority = weapon.GetComponent<MasterArmInput>();
            flow = FindAnyObjectByType<LevelFlow>();
            tint = new MaterialPropertyBlock();
            // In the markup from the first frame. The scene keeps this display off until the entry's beat for it.
            panel = new WeaponsPanel(document.rootVisualElement, weapon);
            panel.Hide();
        }

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            panel?.Hide();
            markers?.Remove();
            markers = null;
            foreach (Renderer reticle in reticles) if (reticle != null) reticle.SetPropertyBlock(null);
            tinted = false;
        }

        private void Bind()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null) return;
            panel = new WeaponsPanel(root, weapon);
            markers?.Remove();
            markers = new TargetMarkers(root);
        }

        /// <summary>Nothing to report under the pause menu, while the ship is being rebuilt or on the results.</summary>
        private bool PanelStandsDown => flow.State != RunState.Playing || !flow.EntryHeld && !weapon.Available;

        private void LateUpdate()
        {
            if (weapon.State == null) return;
            if (panel == null || markers == null || !panel.IsAttached) Bind();
            if (panel == null) return;
            bool armed = authority.Armed;
            WatchMasterArm(armed);
            panel.Show(weapon.State, armed, SafeColour(AxiomPalette.Text), Assembly, PanelStandsDown);
            bool flying = Assembly >= 1f && weapon.Available && Time.timeScale > 0f;
            markers.ShowSafeMark(!armed && flying && authority.WeaponsAvailable && view != null, view, gun.AimPoint.position,
                SafeColour(AxiomPalette.WithAlpha(AxiomPalette.Muted, 0.6f)));
            TintReticles(armed);
            markers.HideBrackets();
            if (flying) markers.ShowBrackets(weapon, armed ? gun.AssistTarget : null, view);
        }

        /// <summary>The moment of arming flares the diamonds; a press while SAFE turns the mark Ember.</summary>
        private void WatchMasterArm(bool armed)
        {
            if (armKnown && armed && !wasArmed && Assembly >= 1f) flareUntil = UiClock.Now + FlareSeconds;
            armKnown = true;
            wasArmed = armed;
            if (authority.RefusedThisFrame) refusedUntil = UiClock.Now + RefusedSeconds;
        }

        private Color SafeColour(Color quiet) => UiClock.Now < refusedUntil ? AxiomPalette.Ember
            : CallForArming ? Color.Lerp(AxiomPalette.Text, AxiomPalette.Light, 0.5f + 0.5f * Mathf.Sin(UiClock.Now * 7f)) : quiet;

        private void TintReticles(bool armed)
        {
            float flare = Mathf.Clamp01((flareUntil - UiClock.Now) / FlareSeconds);
            bool tinting = !armed || flare > 0f;
            if (!tinting && !tinted) return;
            tinted = tinting;
            foreach (Renderer reticle in reticles)
            {
                if (reticle == null) continue;
                if (!tinting)
                {
                    reticle.SetPropertyBlock(null);
                    continue;
                }
                Material material = reticle.sharedMaterial;
                Color own = material.GetColor(BaseColor);
                tint.Clear();
                tint.SetColor(BaseColor, !armed ? own * 0.3f
                    : Color.Lerp(own, AxiomPalette.MaterialColor(material, "_BaseColor", AxiomPalette.Light, 3f), flare));
                reticle.SetPropertyBlock(tint);
            }
        }
    }
}

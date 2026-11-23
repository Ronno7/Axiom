using GeometricFlight.Player;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>Manually sampled ship and instrument reveal. Owns no clock, input, camera or run state. The ship
    /// materializes as authored (<see cref="ShipMaterialization"/>), then relinquishes to ShieldDisplay before flight;
    /// only a local trace remains afterward.</summary>
    [DisallowMultipleComponent]
    public sealed class EntryPresentation : MonoBehaviour
    {
        [SerializeField] private Transform aircraft;
        [SerializeField] private ShipMaterialization materialization;
        [SerializeField] private ShieldDisplay shieldDisplay;
        [SerializeField] private ShipShield shield;
        [SerializeField] private Material traceMaterial;
        [SerializeField] private LevelUi levelUi;
        [Tooltip("How long the ship-local trace keeps running after control begins.")]
        [SerializeField, Min(0.5f)] private float traceSeconds = 4f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // The ship's silhouette: wingtip, nose, wingtip, dorsal peak.
        private static readonly Vector3[] Edges = {
            new Vector3(-2f, 0.01f, -1.24f), new Vector3(0f, 0.02f, 2.5f),
            new Vector3(2f, 0.01f, -1.24f), new Vector3(0f, 0.51f, -0.55f),
            new Vector3(-2f, 0.01f, -1.24f) };

        private LineRenderer registration;
        private Label caption;
        private VisualElement captionRoot, captionLine;
        private string captionText = string.Empty;
        private bool begun, reduced, hullRestored, decorationSuppressed, rebuilding;
        private bool aircraftWasActive, shieldWasEnabled;

        /// <summary>The share of the entry at which the ship starts and finishes materializing.</summary>
        public static float BuildStart => EntrySequence.ShipBeat / EntrySequence.Beats;
        public static float BuildEnd => EntrySequence.ShipSettledBeat / EntrySequence.Beats;

        public bool IsConfigured => aircraft != null && shieldDisplay != null && shield != null && levelUi != null
            && traceMaterial != null && materialization != null && materialization.IsConfigured;

        public float TraceSeconds => traceSeconds;
        public bool HullReady => begun && hullRestored && aircraft != null && aircraft.gameObject.activeInHierarchy;
        public bool HudReady => levelUi != null && levelUi.EntryReady;
        public bool ShieldReady => HullReady && shieldDisplay != null && shieldDisplay.isActiveAndEnabled
            && shield != null && shield.isActiveAndEnabled
            && shield.State != null && !shield.State.IsDepleted && shield.TakesHits;

        public void Begin(bool reducedAnimation)
        {
            ResetPresentation();
            if (!IsConfigured)
            {
                Debug.LogError("Entry presentation references are incomplete.", this);
                return;
            }

            reduced = reducedAnimation;
            aircraftWasActive = aircraft.gameObject.activeSelf;
            shieldWasEnabled = shieldDisplay.enabled;
            shieldDisplay.enabled = false;
            materialization.Show(0f);
            registration = MakeLine("Entry local trace", 0.018f);
            begun = true;
            shield.Hit += OnShieldHit;
            aircraft.gameObject.SetActive(false);
            levelUi.SetEntryVisibility(0f);
            SetCaption(string.Empty);
        }

        /// <summary>Call with the run owner's paused clock. No unscaled timing or scheduled callbacks.</summary>
        public void Sample(float elapsed, float controlTime)
        {
            if (!begun || !float.IsFinite(elapsed) || !float.IsFinite(controlTime) || controlTime <= 0f) return;
            float progress = Mathf.Clamp01(elapsed / controlTime);
            bool reveal = reduced || progress >= BuildStart;
            // Only the build shows the aircraft; once restored, a destroyed ship stays hidden until it is rebuilt.
            if (reveal && !hullRestored && !aircraft.gameObject.activeSelf) aircraft.gameObject.SetActive(true);
            if (!hullRestored && reveal)
            {
                float build = reduced ? 1f : Mathf.InverseLerp(BuildStart, BuildEnd, progress);
                if (build >= 1f) RestoreHull();
                else materialization.Show(build);
            }

            // The instruments draw themselves in turn over the last beats (progress 0.75 = beat 12 of 16):
            // time and targets, the throttle, then the input tag; the weapons instrument is the run owner's.
            if (reduced) levelUi.SetEntryVisibility(1f);
            else levelUi.SetEntryAssembly(Mathf.InverseLerp(0.75f, 0.83f, progress),
                Mathf.InverseLerp(0.78f, 0.93f, progress), Mathf.InverseLerp(0.9f, 0.97f, progress));
            float tail = Mathf.Clamp01((elapsed - controlTime) / traceSeconds);
            bool trace = hullRestored && !reduced && !decorationSuppressed && !levelUi.HasPriorityNotice
                && elapsed >= controlTime && tail < 1f;
            registration.enabled = trace;
            if (trace) DrawRegistration(tail);
            UpdateCaption();
        }

        /// <summary>Finishes the build once. Later cleanup must never overwrite real shield loss or hit blinking.</summary>
        public void RestoreHull()
        {
            if (!begun || hullRestored) return;
            if (materialization != null) materialization.Complete();
            hullRestored = true;
            if (aircraft != null) aircraft.gameObject.SetActive(true);
            if (shieldDisplay != null) shieldDisplay.enabled = shieldWasEnabled; // Rebinds its actual segment state.
        }

        /// <summary>A rebuild after a crash plays the ship's own construction again from nothing; see
        /// <see cref="SampleRebuild"/>. Repeatable; only after the entry's own build has finished.</summary>
        public void BeginRebuild()
        {
            if (!begun || !hullRestored || rebuilding || materialization == null) return;
            rebuilding = true;
            materialization.Show(0f);
        }

        /// <summary>Progress 0 to 1 through the whole authored construction, at whatever pace the rebuild runs.</summary>
        public void SampleRebuild(float progress)
        {
            if (!rebuilding) return;
            materialization.Show(progress);
        }

        /// <summary>The rebuilt ship as a fresh ship: the construction's temporary parts and values are gone.</summary>
        public void EndRebuild()
        {
            if (!rebuilding) return;
            rebuilding = false;
            materialization.Complete();
        }

        /// <summary>The subtitle of a spoken entry report. It gives way to flight feedback and shield changes.</summary>
        public void SetCaption(string text)
        {
            captionText = text ?? string.Empty;
            UpdateCaption();
        }

        public void SuppressDecoration()
        {
            decorationSuppressed = true;
            if (registration != null) registration.enabled = false;
            UpdateCaption();
        }

        public void ResetPresentation()
        {
            if (begun && shield != null) shield.Hit -= OnShieldHit;
            EndRebuild();
            RestoreHull();
            if (begun && aircraft != null) aircraft.gameObject.SetActive(aircraftWasActive);
            if (levelUi != null) levelUi.SetEntryVisibility(1f);
            if (registration != null)
            {
                registration.enabled = false;
                Generated.Release(registration.gameObject);
            }
            registration = null;
            captionLine?.RemoveFromHierarchy();
            captionLine = null;
            caption = null;
            captionRoot = null;
            captionText = string.Empty;
            begun = hullRestored = decorationSuppressed = rebuilding = false;
        }

        private LineRenderer MakeLine(string objectName, float width)
        {
            var line = new GameObject(objectName).AddComponent<LineRenderer>();
            line.gameObject.hideFlags = HideFlags.DontSave;
            line.transform.SetParent(aircraft, false);
            line.sharedMaterial = traceMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            var tint = new MaterialPropertyBlock();
            tint.SetVector(BaseColor, AxiomPalette.ShaderColor(AxiomPalette.Cyan, 0.65f));
            line.SetPropertyBlock(tint);
            return line;
        }

        private void DrawRegistration(float progress)
        {
            // A short edge trace remains attached to the aircraft during unrestricted pitch and roll.
            float front = Mathf.Lerp(0f, Edges.Length - 1f, progress);
            registration.positionCount = 4;
            registration.widthMultiplier = 0.018f * Mathf.Sin(Mathf.PI * progress);
            for (int i = 0; i < 4; i++)
                registration.SetPosition(i, EdgePoint(Mathf.Max(0f, front - 0.3f + i * 0.1f)));
        }

        private static Vector3 EdgePoint(float at)
        {
            int start = Mathf.Clamp(Mathf.FloorToInt(at), 0, Edges.Length - 2);
            return Vector3.Lerp(Edges[start], Edges[start + 1], at - start);
        }

        private void UpdateCaption()
        {
            if (!begun || levelUi == null) return;
            VisualElement root = levelUi.GetComponent<UIDocument>().rootVisualElement?.Q("screen");
            if (root == null) return;
            if (caption == null || captionRoot != root)
            {
                captionLine?.RemoveFromHierarchy();
                captionRoot = root;
                // A full-width line centres the caption at any length; a translate on the label itself goes stale.
                captionLine = new VisualElement { pickingMode = PickingMode.Ignore };
                captionLine.style.position = Position.Absolute;
                captionLine.style.left = captionLine.style.right = 0f;
                captionLine.style.bottom = 88f;
                captionLine.style.alignItems = Align.Center;
                root.Add(captionLine);
                caption = new Label { name = "entry-caption", pickingMode = PickingMode.Ignore };
                caption.style.fontSize = 18f;
                caption.style.letterSpacing = 1f;
                caption.style.color = AxiomPalette.Text;
                caption.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.72f);
                caption.style.paddingLeft = caption.style.paddingRight = 8f;
                caption.style.paddingTop = caption.style.paddingBottom = 3f;
                caption.style.whiteSpace = WhiteSpace.NoWrap;
                caption.style.unityTextAlign = TextAnchor.MiddleCenter;
                captionLine.Add(caption);
            }
            caption.text = captionText;
            bool show = !decorationSuppressed && !levelUi.HasPriorityNotice && captionText.Length > 0;
            caption.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDisable() => ResetPresentation();
        private void OnShieldHit(int remaining) => SuppressDecoration();
    }
}

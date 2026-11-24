using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// The Player Light Jet's construction, as authored (v13): construction traces draw, the interior assembles, the hull
    /// arrives across its surface and the permanent systems light last. Every material reads one build progress, 0 to 1,
    /// standing for the sequence's three reference seconds; the caller owns the clock, so the same build plays in the
    /// entry and, faster, in a rebuild. The interior and traces only exist while building.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipMaterialization : MonoBehaviour
    {
        [Tooltip("The permanent hull and systems: the ship in flight.")]
        [SerializeField] private Renderer[] surfaces;
        [Tooltip("Shown only while the ship builds; they retire by their own timing.")]
        [SerializeField] private Renderer[] interiors;
        [SerializeField] private Renderer[] traces;
        [Tooltip("Lit with the permanent systems, last in the build (the exhaust jets).")]
        [SerializeField] private GameObject[] drive;

        /// <summary>Where the permanent systems light in the authored sequence: 2.88 of its 3 reference seconds.</summary>
        public const float SystemsArrive = 2.88f / 3f;

        private static readonly int Progress = Shader.PropertyToID("_BuildProgress");
        private MaterialPropertyBlock block;

        /// <summary>True between the first <see cref="Show"/> and <see cref="Complete"/>.</summary>
        public bool Building { get; private set; }
        public float BuildProgress { get; private set; } = 1f;

        public bool IsConfigured => surfaces != null && surfaces.Length > 0 && NoGaps(surfaces) && NoGaps(interiors)
            && NoGaps(traces);

        /// <summary>Shows the build at <paramref name="progress"/>: 0 is nothing, 1 is the finished ship. The first call
        /// brings in the interior and traces.</summary>
        public void Show(float progress)
        {
            block ??= new MaterialPropertyBlock();
            if (!Building)
            {
                Building = true;
                SetTemporary(true);
            }
            BuildProgress = Mathf.Clamp01(progress);
            block.SetFloat(Progress, BuildProgress);
            Apply(surfaces, block);
            Apply(interiors, block);
            Apply(traces, block);
            SetDrive(BuildProgress >= SystemsArrive);
        }

        /// <summary>The finished ship: the temporary parts go, and the surfaces return to their own materials' state, so
        /// whatever the shield and hit blink do afterwards is left alone. Does nothing unless a build is showing.</summary>
        public void Complete()
        {
            if (!Building) return;
            Building = false;
            BuildProgress = 1f;
            SetTemporary(false);
            Apply(surfaces, null);
            Apply(interiors, null);
            Apply(traces, null);
            SetDrive(true);
        }

        private void SetDrive(bool lit)
        {
            if (drive == null) return;
            foreach (GameObject part in drive) if (part != null && part.activeSelf != lit) part.SetActive(lit);
        }

        private void SetTemporary(bool shown)
        {
            if (interiors != null) foreach (Renderer r in interiors) if (r != null) r.enabled = shown;
            if (traces != null) foreach (Renderer r in traces) if (r != null) r.enabled = shown;
        }

        private static void Apply(Renderer[] renderers, MaterialPropertyBlock values)
        {
            if (renderers == null) return;
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(values);
        }

        private static bool NoGaps(Renderer[] renderers)
        {
            if (renderers == null) return true;
            foreach (Renderer r in renderers) if (r == null) return false;
            return true;
        }
    }
}

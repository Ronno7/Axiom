using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Procedural sacred-geometry construct:
    /// a counter-rotating star tetrahedron core; a golden-ratio polyhedron cage that tumbles by
    /// torque-free rigid-body dynamics (TumblingBody); a trefoil-knot band; and satellites on
    /// Kepler orbits whose periods follow Kepler's third law (T ∝ a^1.5).
    /// Parts are generated, hidden child objects (never saved); tune everything here.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GeometricConstruct : MonoBehaviour
    {
        public enum CageShape { None, Icosahedron, Dodecahedron }

        [Header("Materials")]
        [SerializeField] private Material primary;
        [SerializeField] private Material secondary;
        [SerializeField] private Material tertiary;

        [Header("Core: star tetrahedron")]
        [SerializeField, Min(0.05f)] private float coreRadius = 1f;
        [Tooltip("Counter-rotation of the two tetrahedra, degrees per second.")]
        [SerializeField] private float coreSpin = 35f;

        [Header("Cage: tumbling polyhedron")]
        [SerializeField] private CageShape cage = CageShape.Icosahedron;
        [SerializeField, Min(0.1f)] private float cageRadius = 1.9f;
        [SerializeField, Min(0.005f)] private float cageEdge = 0.05f;
        [Tooltip("Principal moments of inertia. Three distinct values make the middle axis unstable.")]
        [SerializeField] private Vector3 cageInertia = new Vector3(1f, 2f, 3f);
        [Tooltip("Initial spin about the intermediate axis, radians per second.")]
        [SerializeField] private float cageSpin = 0.8f;

        [Header("Band: trefoil knot")]
        [SerializeField] private bool knot = true;
        [SerializeField, Min(0.1f)] private float knotScale = 1.05f;
        [SerializeField, Min(0.005f)] private float knotThickness = 0.06f;
        [Tooltip("Spin about the knot's 3-fold symmetry axis, degrees per second.")]
        [SerializeField] private float knotSpin = 10f;

        [Header("Satellites: Kepler orbits")]
        [SerializeField, Range(0, 6)] private int satellites = 3;
        [Tooltip("Semi-major axis of the innermost orbit.")]
        [SerializeField, Min(0.1f)] private float orbitSize = 3.2f;
        [SerializeField, Range(0f, 0.9f)] private float eccentricity = 0.5f;
        [Tooltip("Period of the innermost orbit; outer orbits follow Kepler's third law.")]
        [SerializeField, Min(0.5f)] private float orbitPeriod = 6f;
        [SerializeField] private bool drawOrbits = true;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float bob = 0.15f;
        [Tooltip("Varies orientations and phases between instances.")]
        [SerializeField] private int seed = 1;

        private readonly List<GameObject> parts = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Transform pivot, tetraA, tetraB, cageTransform, knotTransform;
        private Transform[] satelliteTransforms = new Transform[0];
        private KeplerOrbit[] orbits = new KeplerOrbit[0];
        private TumblingBody tumble;
        private Quaternion knotTilt;
        private float timeOffset;
        private bool rebuildRequested;

        private void OnEnable() => Build();
        private void OnDisable() => Release();
        private void OnValidate() => rebuildRequested = true;

        private void Update()
        {
            if (rebuildRequested) Build();
            if (pivot == null) return;

            float t = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) + timeOffset;
            pivot.localPosition = new Vector3(0f, bob * Mathf.Sin(t * 1.19f), bob * 0.5f * Mathf.Sin(t * 0.83f));

            Vector3 coreAxis = Vector3.one.normalized;
            if (tetraA != null) tetraA.localRotation = Quaternion.AngleAxis(coreSpin * t, coreAxis);
            if (tetraB != null) tetraB.localRotation = Quaternion.AngleAxis(-coreSpin * t, coreAxis);

            if (cageTransform != null)
            {
                if (Application.isPlaying) tumble.Step(Time.deltaTime);
                cageTransform.localRotation = tumble.Orientation;
                cageTransform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 1.7f));
            }

            if (knotTransform != null)
                knotTransform.localRotation = knotTilt * Quaternion.Euler(0f, 0f, knotSpin * t);

            for (int i = 0; i < satelliteTransforms.Length; i++)
            {
                satelliteTransforms[i].localPosition = orbits[i].PositionAt(t);
                satelliteTransforms[i].localRotation = Quaternion.Euler(0f, 90f * t, 45f);
            }
        }

        private void Build()
        {
            rebuildRequested = false;
            Release();
            var random = new System.Random(seed);
            timeOffset = (float)random.NextDouble() * 50f;
            Material main = primary, second = Pick(secondary, primary), third = Pick(tertiary, second);
            if (main == null) return;

            pivot = Part("Construct", null, null, transform);

            Transform core = Part("Core", null, null, pivot);
            // Stand the star tetrahedron on its 3-fold axis.
            core.localRotation = Quaternion.FromToRotation(Vector3.one, Vector3.up);
            tetraA = Part("TetraA", GeometryMeshes.Tetrahedron(coreRadius, false, "Tetra A"), main, core);
            tetraB = Part("TetraB", GeometryMeshes.Tetrahedron(coreRadius, true, "Tetra B"), second, core);

            if (cage != CageShape.None)
            {
                Vector3[] vertices = cage == CageShape.Icosahedron
                    ? GeometryMeshes.IcosahedronVertices() : GeometryMeshes.DodecahedronVertices();
                cageTransform = Part("Cage",
                    GeometryMeshes.Wireframe(vertices, cageRadius, cageEdge, cageEdge * 1.6f, cage.ToString()), second, pivot);
                // Mostly intermediate-axis spin with a small seeded perturbation: it will flip periodically.
                float wobble = cageSpin * 0.03f;
                tumble = new TumblingBody(cageInertia,
                    new Vector3(wobble * Signed(random), cageSpin, wobble * Signed(random)), RandomRotation(random));
            }

            if (knot)
            {
                knotTilt = RandomRotation(random);
                knotTransform = Part("Knot", GeometryMeshes.ClosedTube(
                    s => GeometryMeshes.TorusKnot(s, 2, 3) * knotScale, 180, knotThickness, 6, "Trefoil"), third, pivot);
            }

            satelliteTransforms = new Transform[satellites];
            orbits = new KeplerOrbit[satellites];
            for (int i = 0; i < satellites; i++)
            {
                float a = orbitSize * (1f + 0.3f * i);
                float period = orbitPeriod * Mathf.Pow(a / orbitSize, 1.5f);
                orbits[i] = new KeplerOrbit(a, eccentricity, period, (float)random.NextDouble(), RandomRotation(random));
                KeplerOrbit orbit = orbits[i];
                if (drawOrbits)
                    Part("Orbit", GeometryMeshes.ClosedTube(orbit.PointAtEccentricAnomaly, 120, 0.012f, 4, "Orbit"), third, pivot);
                satelliteTransforms[i] = Part("Satellite", GeometryMeshes.Octahedron(0.16f, "Satellite"), main, pivot);
            }
        }

        private Transform Part(string partName, Mesh mesh, Material material, Transform parent)
        {
            var go = new GameObject(partName) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent, false);
            if (mesh != null)
            {
                meshes.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            parts.Add(go);
            return go.transform;
        }

        private void Release()
        {
            // Children first, in reverse creation order.
            for (int i = parts.Count - 1; i >= 0; i--) DestroySafely(parts[i]);
            foreach (Mesh mesh in meshes) DestroySafely(mesh);
            parts.Clear();
            meshes.Clear();
            pivot = tetraA = tetraB = cageTransform = knotTransform = null;
            satelliteTransforms = new Transform[0];
        }

        private static void DestroySafely(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static Material Pick(Material preferred, Material fallback) => preferred != null ? preferred : fallback;

        private static float Signed(System.Random random) => (float)random.NextDouble() * 2f - 1f;

        private static Quaternion RandomRotation(System.Random random) => Quaternion.Euler(
            (float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f);
    }
}

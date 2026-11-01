using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Live current on a non-solid obstacle: short jagged arcs crawl along its conductors or jump
    /// between them, re-jittering every frame, and throw sparks where they land. The obstacle keeps
    /// its own look; this only adds light and particles. Purely visual: contact rules live in ShipHull.
    /// Runs only near the camera and freezes with the game clock. Nothing generated here is saved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElectricHazard : MonoBehaviour
    {
        [SerializeField] private Shader glowShader;
        [Tooltip("Meshes the current runs over. Empty uses every child mesh (found again if they change).")]
        [SerializeField] private MeshFilter[] conductors;
        [SerializeField, Min(0f)] private float arcsPerSecond = 14f;
        [SerializeField] private Vector2 arcLifetime = new Vector2(0.05f, 0.14f);
        [SerializeField] private Vector2 arcLength = new Vector2(2f, 9f);
        [Tooltip("Chance an arc jumps to a neighbouring conductor instead of crawling along one.")]
        [SerializeField, Range(0f, 1f)] private float jumpChance = 0.35f;
        [SerializeField, Range(4, 32)] private int arcPoints = 18;
        [Tooltip("Sideways wander of an arc, relative to its length.")]
        [SerializeField, Min(0f)] private float jitter = 0.07f;
        [SerializeField] private Vector2 arcWidth = new Vector2(0.15f, 0.4f);
        [SerializeField, Min(0f)] private float coreIntensity = 1.8f;
        [SerializeField, Min(0)] private int sparksPerArc = 7;
        [Tooltip("Scales spark speed and size with the obstacle (1 suits the original course's scale).")]
        [SerializeField, Min(0.1f)] private float sparkScale = 1f;
        [SerializeField, Min(1f)] private float activeDistance = 450f;

        private const int MaxArcs = 10;

        private sealed class Arc
        {
            public LineRenderer Core, Halo;
            public Vector3 From, To;
            public float Remaining;
        }

        private readonly List<Arc> arcs = new List<Arc>();
        private readonly List<MeshFilter> found = new List<MeshFilter>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Object> generated = new List<Object>();
        private ParticleSystem sparks;
        private Material coreMaterial, haloMaterial, sparkMaterial;
        private Camera view;
        private float pending;

        private void OnEnable()
        {
            if (glowShader == null) { enabled = false; return; }
            // A crisp core with only a faint, narrow halo: current, not fog.
            coreMaterial = LookAtmosphere.GlowMaterial(glowShader, AxiomPalette.Light, 6f, 0f, coreIntensity);
            haloMaterial = LookAtmosphere.GlowMaterial(glowShader, AxiomPalette.Cyan, 3f, 0f, coreIntensity * 0.35f);
            sparkMaterial = LookAtmosphere.GlowMaterial(glowShader, AxiomPalette.Light, 3f, 1f, coreIntensity * 0.8f);
            generated.Add(coreMaterial);
            generated.Add(haloMaterial);
            generated.Add(sparkMaterial);
            for (int i = 0; i < MaxArcs; i++)
                arcs.Add(new Arc { Halo = Line("Arc halo", haloMaterial), Core = Line("Arc core", coreMaterial) });
            sparks = BuildSparks();
        }

        private void OnDisable()
        {
            foreach (Object item in generated) Generated.Release(item);
            generated.Clear();
            arcs.Clear();
            sparks = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // Paused: hold the current frame.
            if (view == null) view = Camera.main;
            bool near = view != null && (view.transform.position - transform.position).sqrMagnitude
                < activeDistance * activeDistance;

            foreach (Arc arc in arcs)
            {
                if (arc.Remaining <= 0f) continue;
                arc.Remaining -= dt;
                bool live = arc.Remaining > 0f && near;
                arc.Core.enabled = arc.Halo.enabled = live;
                if (live) Shape(arc);
            }
            if (!near) return;

            pending += arcsPerSecond * dt;
            while (pending >= 1f)
            {
                pending -= 1f;
                Arc free = arcs.Find(a => a.Remaining <= 0f);
                if (free == null || !Strike(free)) break;
            }
        }

        private bool Strike(Arc arc)
        {
            List<MeshFilter> pool = Conductors();
            if (pool.Count == 0) return false;
            MeshFilter a = pool[Random.Range(0, pool.Count)];
            if (!SurfacePoint(a, out Vector3 from)) return false;
            float length = Random.Range(arcLength.x, arcLength.y);
            Vector3 to = from + Random.onUnitSphere * length;
            bool landed = false;
            for (int attempt = 0; attempt < 5 && !landed; attempt++)
            {
                MeshFilter b = Random.value < jumpChance ? pool[Random.Range(0, pool.Count)] : a;
                if (!SurfacePoint(b, out Vector3 candidate)) continue;
                float d = Vector3.Distance(from, candidate);
                if (d >= arcLength.x * 0.5f && d <= arcLength.y) { to = candidate; landed = true; }
            }
            arc.From = from;
            arc.To = to;
            arc.Remaining = Random.Range(arcLifetime.x, arcLifetime.y);
            float width = Random.Range(arcWidth.x, arcWidth.y);
            arc.Halo.widthMultiplier = width;
            arc.Core.widthMultiplier = width * 0.45f;
            Shape(arc);
            arc.Core.enabled = arc.Halo.enabled = true;
            Spray(to);
            if (Random.value < 0.4f) Spray(from);
            return true;
        }

        /// <summary>A fresh jagged path every frame: the arc crackles while it lives.</summary>
        private void Shape(Arc arc)
        {
            Vector3 along = arc.To - arc.From;
            float length = along.magnitude;
            Vector3 side = Vector3.Cross(along, Random.onUnitSphere).normalized;
            Vector3 up = Vector3.Cross(along, side).normalized;
            for (int i = 0; i < arcPoints; i++)
            {
                float t = i / (arcPoints - 1f);
                float wander = jitter * length * Mathf.Sin(Mathf.PI * t);
                Vector3 p = arc.From + along * t + (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * wander;
                arc.Core.SetPosition(i, p);
                arc.Halo.SetPosition(i, p);
            }
        }

        private void Spray(Vector3 at)
        {
            var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = false };
            for (int i = 0; i < sparksPerArc; i++)
            {
                emit.velocity = Random.onUnitSphere * Random.Range(3f, 11f) * sparkScale;
                emit.startLifetime = Random.Range(0.15f, 0.45f);
                emit.startSize = Random.Range(0.08f, 0.18f) * sparkScale;
                sparks.Emit(emit, 1);
            }
        }

        private List<MeshFilter> Conductors()
        {
            found.Clear();
            if (conductors != null)
                foreach (MeshFilter m in conductors) if (Visible(m)) found.Add(m);
            if (found.Count == 0)
                // Arcs and sparks have no MeshFilter, so every child mesh is part of the obstacle.
                foreach (MeshFilter m in GetComponentsInChildren<MeshFilter>())
                    if (m.sharedMesh != null && Visible(m)) found.Add(m);
            return found;
        }

        /// <summary>No current on parts that aren't shown yet (built with the passage).</summary>
        private static bool Visible(MeshFilter m) =>
            m != null && m.gameObject.activeInHierarchy && m.TryGetComponent(out MeshRenderer r) && r.enabled;

        /// <summary>A random point on the mesh as it is now (iris blades rebuild every frame).</summary>
        private bool SurfacePoint(MeshFilter filter, out Vector3 point)
        {
            point = filter.transform.position;
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) return false;
            mesh.GetVertices(vertices);
            mesh.GetTriangles(triangles, 0);
            if (triangles.Count < 3) return false;
            int t = Random.Range(0, triangles.Count / 3) * 3;
            float u = Random.value, v = Random.value;
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            Vector3 local = vertices[triangles[t]] + (vertices[triangles[t + 1]] - vertices[triangles[t]]) * u
                + (vertices[triangles[t + 2]] - vertices[triangles[t]]) * v;
            point = filter.transform.TransformPoint(local);
            return true;
        }

        private LineRenderer Line(string partName, Material material)
        {
            var go = new GameObject(partName) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = arcPoints;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            generated.Add(go);
            return line;
        }

        private ParticleSystem BuildSparks()
        {
            var go = new GameObject("Sparks") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f;
            main.maxParticles = 800;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(AxiomPalette.White, 0f), new GradientColorKey(AxiomPalette.White, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            generated.Add(go);
            return system;
        }
    }
}

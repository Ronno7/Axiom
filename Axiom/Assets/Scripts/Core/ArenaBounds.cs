using GeometricFlight.Player;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Core
{
    /// <summary>A round arena around this transform: the floor disc, the lowest flying height and the
    /// ceiling that ShipFlight keeps the ship inside. Draws the edge as thin light lines (floor ring,
    /// ceiling ring and evenly spaced posts) so the boundary is visible before the ship reaches it.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaBounds : MonoBehaviour
    {
        [SerializeField, Min(10f)] private float radius = 160f;
        [Tooltip("Lowest flying height above this transform, metres.")]
        [SerializeField, Min(0.5f)] private float minAltitude = 2f;
        [SerializeField, Min(5f)] private float ceiling = 100f;

        [Header("Edge lines")]
        [SerializeField] private Material lineMaterial;
        [SerializeField, Range(4, 72)] private int posts = 36;
        [SerializeField, Min(0f)] private float ringHeightStep = 25f;

        private Mesh mesh;

        public ArenaLimits Limits => new ArenaLimits(transform.position, radius, minAltitude, ceiling);
        public float Radius => radius;
        public float Ceiling => ceiling;

        private void Awake()
        {
            if (lineMaterial == null) return;
            mesh = BuildEdge();
            var go = new GameObject("ArenaEdge");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = lineMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private Mesh BuildEdge()
        {
            const int segments = 144;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();
            for (float h = 0f; h <= ceiling + 0.01f; h += ringHeightStep > 0f ? ringHeightStep : ceiling)
            {
                int start = vertices.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Sin(a) * radius, h, Mathf.Cos(a) * radius));
                    indices.Add(start + i);
                    indices.Add(start + (i + 1) % segments);
                }
                if (ringHeightStep <= 0f) break;
            }
            for (int i = 0; i < posts; i++)
            {
                float a = i * Mathf.PI * 2f / posts;
                var foot = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                indices.Add(vertices.Count);
                vertices.Add(foot);
                indices.Add(vertices.Count);
                vertices.Add(foot + Vector3.up * ceiling);
            }
            var m = new Mesh { name = "Arena edge (generated)", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(vertices);
            m.SetIndices(indices, MeshTopology.Lines, 0);
            return m;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = AxiomPalette.WithAlpha(AxiomPalette.Cyan, 0.5f);
            Vector3 c = transform.position;
            const int n = 72;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                var p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                Gizmos.DrawLine(c + p0, c + p1);
                Gizmos.DrawLine(c + p0 + Vector3.up * ceiling, c + p1 + Vector3.up * ceiling);
            }
        }
    }
}

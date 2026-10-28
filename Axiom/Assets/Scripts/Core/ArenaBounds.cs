using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Core
{
    /// <summary>A round arena around this transform: the floor disc, the lowest flying height and the
    /// ceiling that ArenaFlight keeps the ship inside. Draws the edge as thin light lines (floor ring,
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
        [Tooltip("Optional translucent wall (Arena Wall shader) on the edge, brightening near Focus.")]
        [SerializeField] private Material wallMaterial;
        [Tooltip("What the wall brightens near (the player ship).")]
        [SerializeField] private Transform focus;
        [SerializeField, Range(4, 72)] private int posts = 36;
        [SerializeField, Min(0f)] private float ringHeightStep = 25f;

        private Mesh mesh, wallMesh;
        private static readonly int FocusId = Shader.PropertyToID("_ArenaFocus");

        public ArenaLimits Limits => new ArenaLimits(transform.position, radius, minAltitude, ceiling);
        public float Radius => radius;
        public float Ceiling => ceiling;

        private void Awake()
        {
            if (lineMaterial != null)
            {
                mesh = BuildEdge();
                AddChild("ArenaEdge", mesh, lineMaterial);
            }
            if (wallMaterial != null)
            {
                wallMesh = BuildWall();
                AddChild("ArenaWall", wallMesh, wallMaterial);
            }
        }

        private void LateUpdate()
        {
            if (focus != null) Shader.SetGlobalVector(FocusId, focus.position);
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (wallMesh != null) Destroy(wallMesh);
        }

        private void AddChild(string label, Mesh m, Material material)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>An open cylinder from the floor to the ceiling; uv.y is 0 at the floor and 1 at the ceiling.</summary>
        private Mesh BuildWall()
        {
            const int segments = 144;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                vertices[i * 2] = p;
                vertices[i * 2 + 1] = p + Vector3.up * ceiling;
                uv[i * 2] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
                if (i == segments) break;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var m = new Mesh { name = "Arena wall (generated)", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = vertices;
            m.uv = uv;
            m.triangles = triangles;
            m.RecalculateBounds();
            return m;
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
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
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

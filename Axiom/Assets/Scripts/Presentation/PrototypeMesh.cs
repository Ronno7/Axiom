using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Small editable blockout meshes. Runs in the editor as well as Play Mode.
    /// This component supplies geometry (and an optional MeshCollider's mesh) only;
    /// it has no gameplay behavior.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PrototypeMesh : MonoBehaviour
    {
        public enum Shape { Aircraft, Octahedron, PolygonRing }

        [SerializeField] private Shape shape;
        [SerializeField, Range(3, 24)] private int sides = 6;
        [SerializeField, Min(0.1f)] private float radius = 1.5f;
        [SerializeField, Min(0.02f)] private float width = 0.14f;
        [SerializeField, Min(0.02f)] private float depth = 0.16f;

        private Mesh generatedMesh;
        private bool rebuildRequested;

        private void OnEnable() => Rebuild();

        // Mesh work is deferred from OnValidate because Unity may invoke it
        // while deserializing assets, outside the normal editor update.
        private void OnValidate() => rebuildRequested = true;

        private void Update()
        {
            if (rebuildRequested) Rebuild();
        }

        [ContextMenu("Rebuild prototype mesh")]
        public void Rebuild()
        {
            rebuildRequested = false;
            ReleaseMesh();
            var vertices = new List<Vector3>();
            var indices = new List<int>();

            switch (shape)
            {
                case Shape.Aircraft: BuildAircraft(vertices, indices); break;
                case Shape.Octahedron: BuildOctahedron(vertices, indices); break;
                case Shape.PolygonRing: BuildRing(vertices, indices); break;
            }

            generatedMesh = new Mesh
            {
                name = "Prototype " + shape,
                hideFlags = HideFlags.HideAndDontSave
            };
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetTriangles(indices, 0);
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            // Optional collision: generated meshes are not saved, so assign at runtime.
            if (TryGetComponent(out MeshCollider meshCollider))
                meshCollider.sharedMesh = generatedMesh;
        }

        private static void Triangle(List<Vector3> vertices, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        private static void Quad(List<Vector3> vertices, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(vertices, indices, a, b, c);
            Triangle(vertices, indices, a, c, d);
        }

        private static void BuildAircraft(List<Vector3> vertices, List<int> indices)
        {
            // Forward is +Z. Duplicated face vertices produce flat shading.
            Vector3 left = new Vector3(-2f, 0f, -1.4f);
            Vector3 nose = new Vector3(0f, 0f, 2.5f);
            Vector3 right = new Vector3(2f, 0f, -1.4f);
            Vector3 ridge = new Vector3(0f, 0.5f, -0.5f);
            Vector3 belly = new Vector3(0f, -0.22f, -0.5f);
            Triangle(vertices, indices, left, nose, ridge);
            Triangle(vertices, indices, nose, right, ridge);
            Triangle(vertices, indices, right, left, ridge);
            Triangle(vertices, indices, left, belly, nose);
            Triangle(vertices, indices, nose, belly, right);
            Triangle(vertices, indices, right, belly, left);
        }

        private static void BuildOctahedron(List<Vector3> vertices, List<int> indices)
        {
            Vector3[] equator = { Vector3.forward, Vector3.right,
                Vector3.back, Vector3.left };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = equator[i], b = equator[(i + 1) % 4];
                Triangle(vertices, indices, Vector3.up, a, b);
                Triangle(vertices, indices, Vector3.down, b, a);
            }
        }

        private void BuildRing(List<Vector3> vertices, List<int> indices)
        {
            int count = Mathf.Clamp(sides, 3, 24);
            float r = Mathf.Max(0.1f, radius);
            float halfWidth = Mathf.Clamp(width * 0.5f, 0.01f, r * 0.45f);
            float halfDepth = Mathf.Max(0.01f, depth * 0.5f);
            Vector3 z = Vector3.forward * halfDepth;
            for (int i = 0; i < count; i++)
            {
                float a = 2f * Mathf.PI * i / count;
                float b = 2f * Mathf.PI * (i + 1) / count;
                Vector3 u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 v = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
                Vector3 ao = u * (r + halfWidth), ai = u * (r - halfWidth);
                Vector3 bo = v * (r + halfWidth), bi = v * (r - halfWidth);
                Quad(vertices, indices, ao + z, bo + z, bi + z, ai + z);
                Quad(vertices, indices, ai - z, bi - z, bo - z, ao - z);
                Quad(vertices, indices, ao - z, bo - z, bo + z, ao + z);
                Quad(vertices, indices, ai + z, bi + z, bi - z, ai - z);
            }
        }

        private void OnDisable() => ReleaseMesh();

        private void ReleaseMesh()
        {
            if (generatedMesh == null) return;
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == generatedMesh)
                filter.sharedMesh = null;
            if (TryGetComponent(out MeshCollider meshCollider) && meshCollider.sharedMesh == generatedMesh)
                meshCollider.sharedMesh = null;
            if (Application.isPlaying) Destroy(generatedMesh);
            else DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
    }
}

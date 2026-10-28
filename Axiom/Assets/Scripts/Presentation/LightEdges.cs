using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>Traces the 12 edges of this object's unit box (a scaled cube) in self-lit lines, the Grid's
    /// architecture style: dark glass bodies outlined in light. Optional rings around the box at even heights
    /// give tall towers a sense of scale. Built when play starts.</summary>
    [DisallowMultipleComponent]
    public sealed class LightEdges : MonoBehaviour
    {
        [SerializeField] private Material material;
        [Tooltip("Horizontal bands around the box, evenly spaced up its height. 0 = edges only.")]
        [SerializeField, Range(0, 40)] private int bands;

        private Mesh mesh;

        private void Awake()
        {
            if (material == null) return;
            mesh = Build(bands);
            var lines = new GameObject("LightEdges");
            lines.transform.SetParent(transform, false);
            lines.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = lines.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private static Mesh Build(int bands)
        {
            var v = new System.Collections.Generic.List<Vector3>();
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            for (int a = 0; a < 8; a++)
            for (int b = a + 1; b < 8; b++)
            {
                int diff = a ^ b;
                if (diff != 1 && diff != 2 && diff != 4) continue; // edges differ in exactly one axis
                v.Add(corners[a]);
                v.Add(corners[b]);
            }
            for (int k = 1; k <= bands; k++)
            {
                float y = -0.5f + k / (float)(bands + 1);
                var ring = new[] { new Vector3(-0.5f, y, -0.5f), new Vector3(0.5f, y, -0.5f),
                                   new Vector3(0.5f, y, 0.5f), new Vector3(-0.5f, y, 0.5f) };
                for (int i = 0; i < 4; i++)
                {
                    v.Add(ring[i]);
                    v.Add(ring[(i + 1) % 4]);
                }
            }
            var indices = new int[v.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            var m = new Mesh { name = "Light edges (generated)", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(v);
            m.SetIndices(indices, MeshTopology.Lines, 0);
            return m;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// A generated enclosed rail passage along world +Z: a beveled tunnel of dark surfaces, light ribs,
    /// edge lines and floor markings that continue the menu runway. It builds itself as three world-Z
    /// fronts advance (markings, ribs, surfaces); with no caller it stays complete. Wall colliders are
    /// generated with it and marked lethal: touching a wall destroys the ship. Proximity warns first.
    /// The floor itself is a separate scene object. Nothing generated here is saved.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [SelectionBase] // Clicking a generated (hidden) part selects this object.
    public sealed class EnclosedPassage : MonoBehaviour
    {
        [Header("Shape (local metres)")]
        [SerializeField, Min(4f)] private float width = 64f;
        [SerializeField, Min(4f)] private float height = 34f;
        [Tooltip("Bevel between each wall and the ceiling.")]
        [SerializeField, Min(0f)] private float bevel = 10f;
        [Tooltip("Open strip along the ceiling's centre; 0 closes it.")]
        [SerializeField, Min(0f)] private float ceilingSlot = 14f;
        [SerializeField] private float startZ = -150f;
        [SerializeField] private float endZ = 900f;
        [SerializeField, Min(2f)] private float ribSpacing = 24f;
        [SerializeField, Min(0.05f)] private float ribThickness = 0.4f;
        [SerializeField, Min(0.02f)] private float edgeThickness = 0.25f;

        [Header("Floor markings (match the menu runway)")]
        [SerializeField, Min(0f)] private float seamOffset = 8f;
        [SerializeField, Min(1f)] private float markerSpacing = 12f;
        [Tooltip("World Z of one marker, modulo the spacing, so markers line up with the menu's.")]
        [SerializeField] private float markerPhase = 4f;
        [SerializeField, Min(0.1f)] private float markerWidth = 16f;

        [Header("Construction")]
        [Tooltip("Front travel over which one rib draws from both feet to its apex.")]
        [SerializeField, Min(0.1f)] private float ribDrawLength = 40f;
        [Tooltip("How far the edge lines race ahead of the ribs, once they have overtaken them.")]
        [SerializeField] private float edgeLead = 60f;
        [SerializeField] private Material surfaceMaterial, ribMaterial, edgeMaterial, markingMaterial;

        private static readonly int FrontId = Shader.PropertyToID("_BuildFront");
        private static readonly int DrawLengthId = Shader.PropertyToID("_DrawLength");
        private const float Complete = 1e7f;

        private readonly List<Object> generated = new List<Object>();
        private Renderer surfaces, roof, ribs, edges, markings;
        private MaterialPropertyBlock block;
        private bool rebuildRequested;
        private float markingFront = Complete, ribFront = Complete, surfaceFront = Complete, edgeFront = Complete;

        public float Width => width;
        public float Height => height;
        /// <summary>World Z of the mouth and the far end.</summary>
        public float MouthZ => transform.TransformPoint(0f, 0f, startZ).z;
        public float EndZ => transform.TransformPoint(0f, 0f, endZ).z;
        public int RibCount => RibPositions(startZ, endZ, ribSpacing).Count;
        /// <summary>World Z of rib <paramref name="index"/>, counting from the mouth.</summary>
        public float RibZ(int index) => transform.TransformPoint(0f, 0f, startZ + index * ribSpacing).z;
        public bool IsComplete => surfaceFront >= EndZ && ribFront >= EndZ + ribDrawLength && markingFront >= EndZ;

        private void OnEnable() => Rebuild();
        private void OnDisable() => Release();
        private void OnValidate() => rebuildRequested = true;

        private void Update()
        {
            if (rebuildRequested) Rebuild();
        }

        /// <summary>Advances construction. Each value is a world Z; geometry before it is built. The edge lines lead the
        /// ribs, but no further than <paramref name="edgesLimit"/>, so they can start behind the camera and overtake.</summary>
        public void SetFronts(float markingsFront, float ribsFront, float surfacesFront, float edgesLimit = Complete)
        {
            // A front that has not started is -infinity; keep GPU values finite.
            markingFront = Mathf.Clamp(markingsFront, -Complete, Complete);
            ribFront = Mathf.Clamp(ribsFront, -Complete, Complete);
            surfaceFront = Mathf.Clamp(surfacesFront, -Complete, Complete);
            edgeFront = Mathf.Min(ribFront + edgeLead, Mathf.Clamp(edgesLimit, -Complete, Complete));
            ApplyFronts();
        }

        public void ShowComplete() => SetFronts(Complete, Complete, Complete);
        public void HideAll() => SetFronts(-Complete, -Complete, -Complete);

        /// <summary>The cross-section from the left foot over the ceiling to the right foot (x, y).</summary>
        public static Vector2[] Outline(float width, float height, float bevel)
        {
            float half = width * 0.5f;
            bevel = Mathf.Clamp(bevel, 0f, Mathf.Min(half, height) * 0.9f);
            return new[]
            {
                new Vector2(-half, 0f), new Vector2(-half, height - bevel), new Vector2(-half + bevel, height),
                new Vector2(half - bevel, height), new Vector2(half, height - bevel), new Vector2(half, 0f),
            };
        }

        /// <summary>Drawing parameter at each outline point: 0 at both feet, 1 at the ceiling's centre,
        /// so a rib's legs rise together and meet overhead.</summary>
        public static float[] PathParameters(Vector2[] outline)
        {
            var arc = new float[outline.Length];
            for (int i = 1; i < outline.Length; i++) arc[i] = arc[i - 1] + Vector2.Distance(outline[i - 1], outline[i]);
            float halfLength = arc[arc.Length - 1] * 0.5f;
            var parameters = new float[outline.Length];
            for (int i = 0; i < outline.Length; i++)
                parameters[i] = halfLength > 0f ? Mathf.Min(arc[i], arc[arc.Length - 1] - arc[i]) / halfLength : 0f;
            return parameters;
        }

        /// <summary>Local Z of every rib, mouth first, the last at the far end.</summary>
        public static List<float> RibPositions(float startZ, float endZ, float spacing)
        {
            var positions = new List<float>();
            if (spacing <= 0f || endZ < startZ) return positions;
            for (float z = startZ; z < endZ - spacing * 0.25f; z += spacing) positions.Add(z);
            positions.Add(endZ);
            return positions;
        }

        [ContextMenu("Rebuild passage")]
        public void Rebuild()
        {
            rebuildRequested = false;
            Release();
            if (surfaceMaterial == null || ribMaterial == null || edgeMaterial == null || markingMaterial == null) return;
            block ??= new MaterialPropertyBlock();

            Vector2[] outline = Outline(width, height, bevel);
            float[] path = PathParameters(outline);
            List<float> ribZ = RibPositions(startZ, endZ, ribSpacing);

            surfaces = Part("Surfaces", BuildSurfaces(outline, ribZ, false), surfaceMaterial);
            roof = Part("Roof", BuildSurfaces(outline, ribZ, true), surfaceMaterial);
            ribs = Part("Ribs", BuildRibs(outline, path, ribZ), ribMaterial);
            edges = Part("Edges", BuildEdges(outline, ribZ), edgeMaterial);
            markings = Part("Floor markings", BuildMarkings(), markingMaterial);
            BuildColliders(outline);
            ApplyFronts();
        }

        private void ApplyFronts()
        {
            if (block == null) return;
            Apply(surfaces, surfaceFront, 1f);
            Apply(roof, surfaceFront, 1f);
            Apply(ribs, ribFront, ribDrawLength);
            // Edge and marking lines draw along Z; the draw length sets their glowing tip (8% of it).
            Apply(edges, edgeFront, 100f);
            Apply(markings, markingFront, 60f);
        }

        private void Apply(Renderer target, float front, float drawLength)
        {
            if (target == null) return;
            block.Clear();
            block.SetFloat(FrontId, front);
            block.SetFloat(DrawLengthId, drawLength);
            target.SetPropertyBlock(block);
        }

        /// <summary>The walls (<paramref name="roofOnly"/> false) or the roof: the bevels and ceiling, every segment
        /// that doesn't touch the floor.</summary>
        private Mesh BuildSurfaces(Vector2[] outline, List<float> ribZ, bool roofOnly)
        {
            var mesh = new MeshBuilder();
            float slot = Mathf.Min(ceilingSlot, Mathf.Max(0f, width - 2f * bevel) - 1f);
            for (int s = 0; s < outline.Length - 1; s++)
            {
                Vector2 a = outline[s], b = outline[s + 1];
                bool isRoof = a.y > 0.01f && b.y > 0.01f;
                if (isRoof != roofOnly) continue;
                bool ceiling = Mathf.Approximately(a.y, height) && Mathf.Approximately(b.y, height);
                for (int r = 0; r < ribZ.Count - 1; r++)
                {
                    if (ceiling && slot > 0f)
                    {
                        mesh.Quad(a, new Vector2(-slot * 0.5f, height), ribZ[r], ribZ[r + 1]);
                        mesh.Quad(new Vector2(slot * 0.5f, height), b, ribZ[r], ribZ[r + 1]);
                    }
                    else mesh.Quad(a, b, ribZ[r], ribZ[r + 1]);
                }
            }
            return mesh.ToMesh(roofOnly ? "Passage roof" : "Passage walls");
        }

        private Mesh BuildRibs(Vector2[] outline, float[] path, List<float> ribZ)
        {
            var mesh = new MeshBuilder();
            foreach (float z in ribZ)
            {
                float worldZ = transform.TransformPoint(0f, 0f, z).z;
                for (int s = 0; s < outline.Length - 1; s++)
                    mesh.CrossBar(outline[s], outline[s + 1], z, ribThickness, path[s], path[s + 1], worldZ);
            }
            return mesh.ToMesh("Passage ribs");
        }

        private Mesh BuildEdges(Vector2[] outline, List<float> ribZ)
        {
            var mesh = new MeshBuilder();
            var corners = new List<Vector2>(outline);
            float slot = Mathf.Min(ceilingSlot, Mathf.Max(0f, width - 2f * bevel) - 1f);
            if (slot > 0f)
            {
                corners.Add(new Vector2(-slot * 0.5f, height));
                corners.Add(new Vector2(slot * 0.5f, height));
            }
            foreach (Vector2 corner in corners)
                for (int r = 0; r < ribZ.Count - 1; r++)
                    mesh.LongBar(corner, ribZ[r], ribZ[r + 1], edgeThickness, transform);
            return mesh.ToMesh("Passage edges");
        }

        private Mesh BuildMarkings()
        {
            var mesh = new MeshBuilder();
            const float lift = 0.02f;
            for (float z = startZ; z < endZ; z += ribSpacing)
            {
                float to = Mathf.Min(endZ, z + ribSpacing);
                mesh.FloorStrip(-seamOffset - 0.05f, -seamOffset + 0.05f, z, to, lift, transform);
                mesh.FloorStrip(seamOffset - 0.05f, seamOffset + 0.05f, z, to, lift, transform);
            }
            // Markers keep the menu's world phase, so they continue its scrolling floor exactly.
            float offset = Mathf.Repeat(markerPhase - MouthZ, markerSpacing);
            for (float z = startZ + offset; z < endZ; z += markerSpacing)
                mesh.FloorStrip(-markerWidth * 0.5f, markerWidth * 0.5f, z - 0.03f, z + 0.03f, lift, transform);
            return mesh.ToMesh("Passage floor markings");
        }

        private void BuildColliders(Vector2[] outline)
        {
            float length = endZ - startZ, center = (startZ + endZ) * 0.5f;
            const float thickness = 1f;
            for (int s = 0; s < outline.Length - 1; s++)
            {
                Vector2 a = outline[s], b = outline[s + 1];
                Vector2 along = (b - a).normalized;
                // The outline runs clockwise when seen from behind, so outward is to its left.
                Vector2 outward = new Vector2(-along.y, along.x);
                Vector2 mid = (a + b) * 0.5f + outward * thickness * 0.5f;
                var part = new GameObject("Wall collider " + s) { hideFlags = HideFlags.HideAndDontSave };
                part.layer = gameObject.layer;
                part.transform.SetParent(transform, false);
                part.transform.localPosition = new Vector3(mid.x, mid.y, center);
                part.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
                part.AddComponent<Player.LethalSurface>();
                var box = part.AddComponent<BoxCollider>();
                box.size = new Vector3(Vector2.Distance(a, b), thickness, length);
                generated.Add(part);
            }
        }

        private Renderer Part(string partName, Mesh mesh, Material material)
        {
            // Shown (greyed, never saved) so a Scene view click can select it; [SelectionBase] then selects the owner.
            var part = new GameObject(partName) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            generated.Add(mesh);
            generated.Add(part);
            return renderer;
        }

        private void Release()
        {
            foreach (Object item in generated) Generated.Release(item);
            generated.Clear();
            surfaces = roof = ribs = edges = markings = null;
        }

        /// <summary>Small mesh accumulator. UV.x is the drawing parameter, UV.y the world Z it starts at.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> indices = new List<int>();

            public void Quad(Vector2 a, Vector2 b, float z0, float z1)
            {
                int i = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, z0)); vertices.Add(new Vector3(b.x, b.y, z0));
                vertices.Add(new Vector3(b.x, b.y, z1)); vertices.Add(new Vector3(a.x, a.y, z1));
                for (int k = 0; k < 4; k++) uvs.Add(Vector2.zero);
                indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            /// <summary>A bar across the passage from a to b at z, drawing from pa to pb.</summary>
            public void CrossBar(Vector2 a, Vector2 b, float z, float thickness, float pa, float pb, float worldZ)
            {
                Vector2 along = (b - a).normalized, normal = new Vector2(-along.y, along.x) * (thickness * 0.5f);
                float h = thickness * 0.5f;
                Box(new[]
                {
                    new Vector3(a.x - normal.x, a.y - normal.y, z - h), new Vector3(a.x + normal.x, a.y + normal.y, z - h),
                    new Vector3(a.x + normal.x, a.y + normal.y, z + h), new Vector3(a.x - normal.x, a.y - normal.y, z + h),
                    new Vector3(b.x - normal.x, b.y - normal.y, z - h), new Vector3(b.x + normal.x, b.y + normal.y, z - h),
                    new Vector3(b.x + normal.x, b.y + normal.y, z + h), new Vector3(b.x - normal.x, b.y - normal.y, z + h),
                }, k => new Vector2(k < 4 ? pa : pb, worldZ));
            }

            /// <summary>A bar along Z at a cross-section point; it races with the front (UV.x 0).</summary>
            public void LongBar(Vector2 at, float z0, float z1, float thickness, Transform frame)
            {
                float h = thickness * 0.5f;
                float w0 = frame.TransformPoint(0f, 0f, z0).z, w1 = frame.TransformPoint(0f, 0f, z1).z;
                Box(new[]
                {
                    new Vector3(at.x - h, at.y - h, z0), new Vector3(at.x + h, at.y - h, z0),
                    new Vector3(at.x + h, at.y + h, z0), new Vector3(at.x - h, at.y + h, z0),
                    new Vector3(at.x - h, at.y - h, z1), new Vector3(at.x + h, at.y - h, z1),
                    new Vector3(at.x + h, at.y + h, z1), new Vector3(at.x - h, at.y + h, z1),
                }, k => new Vector2(0f, k < 4 ? w0 : w1));
            }

            /// <summary>A flat strip lying on the floor (upward face only).</summary>
            public void FloorStrip(float x0, float x1, float z0, float z1, float y, Transform frame)
            {
                int i = vertices.Count;
                float w0 = frame.TransformPoint(0f, 0f, z0).z, w1 = frame.TransformPoint(0f, 0f, z1).z;
                vertices.Add(new Vector3(x0, y, z0)); vertices.Add(new Vector3(x1, y, z0));
                vertices.Add(new Vector3(x1, y, z1)); vertices.Add(new Vector3(x0, y, z1));
                uvs.Add(new Vector2(0f, w0)); uvs.Add(new Vector2(0f, w0));
                uvs.Add(new Vector2(0f, w1)); uvs.Add(new Vector2(0f, w1));
                indices.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            private void Box(Vector3[] corners, System.Func<int, Vector2> uv)
            {
                int i = vertices.Count;
                for (int k = 0; k < 8; k++) { vertices.Add(corners[k]); uvs.Add(uv(k)); }
                int[] faces = { 0, 1, 2, 3, 4, 7, 6, 5, 0, 4, 5, 1, 1, 5, 6, 2, 2, 6, 7, 3, 3, 7, 4, 0 };
                for (int f = 0; f < faces.Length; f += 4)
                    indices.AddRange(new[] { i + faces[f], i + faces[f + 1], i + faces[f + 2],
                        i + faces[f], i + faces[f + 2], i + faces[f + 3] });
            }

            public Mesh ToMesh(string meshName)
            {
                var mesh = new Mesh { name = meshName, hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

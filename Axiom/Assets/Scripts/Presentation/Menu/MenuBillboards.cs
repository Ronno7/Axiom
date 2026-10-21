using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>A set of soft camera-facing dots in one mesh (the menu's stars and dust). Brightness rides in the
    /// vertex colour's alpha for the Additive Glow shader. Centres can move; call <see cref="Write"/> after.</summary>
    public sealed class MenuBillboards
    {
        public readonly Vector3[] Centers;
        public readonly Mesh Mesh;
        private readonly float[] sizes;
        private readonly Vector3[] vertices;

        public MenuBillboards(string name, System.Random random, int count, float spread, Vector2 height, Vector2 depth,
            Vector2 size, Vector2 brightness, float brightnessCurve, Vector3 eye)
        {
            Centers = new Vector3[count];
            sizes = new float[count];
            vertices = new Vector3[count * 4];
            var colors = new Color[count * 4];
            var uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                Centers[i] = eye + new Vector3(Range(random, -spread, spread), Range(random, height.x, height.y) - eye.y,
                    Range(random, depth.x, depth.y));
                sizes[i] = Range(random, size.x, size.y);
                float b = Mathf.Lerp(brightness.x, brightness.y, Mathf.Pow((float)random.NextDouble(), brightnessCurve));
                for (int j = 0; j < 4; j++) colors[i * 4 + j] = new Color(1f, 1f, 1f, b);
                uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(1, 0);
                uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(0, 1);
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            Mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            Write(eye);
            Mesh.colors = colors;
            Mesh.uv = uv;
            Mesh.triangles = triangles;
            Mesh.RecalculateBounds();
        }

        /// <summary>Rebuilds the quads around the current centres, each facing the eye.</summary>
        public void Write(Vector3 eye)
        {
            for (int i = 0; i < Centers.Length; i++)
            {
                Vector3 forward = Centers[i] - eye;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                right = right.sqrMagnitude > 1e-8f ? right.normalized * sizes[i] : Vector3.right * sizes[i];
                Vector3 up = Vector3.Cross(forward, right).normalized * sizes[i];
                int v = i * 4;
                vertices[v] = Centers[i] - right - up;
                vertices[v + 1] = Centers[i] + right - up;
                vertices[v + 2] = Centers[i] + right + up;
                vertices[v + 3] = Centers[i] - right + up;
            }
            Mesh.vertices = vertices;
        }

        private static float Range(System.Random random, float a, float b) => a + (float)random.NextDouble() * (b - a);
    }
}

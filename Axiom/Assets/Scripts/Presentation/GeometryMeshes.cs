using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Procedural meshes for geometric constructs. Every face has its own vertices,
    /// so normals are flat and the facet shading of the project's shader reads clearly.</summary>
    public static class GeometryMeshes
    {
        public static readonly float Phi = (1f + Mathf.Sqrt(5f)) * 0.5f;

        /// <summary>12 vertices of a unit-circumradius icosahedron: cyclic permutations of (0, ±1, ±φ).</summary>
        public static Vector3[] IcosahedronVertices()
        {
            var v = new List<Vector3>();
            foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -Phi, Phi })
            {
                v.Add(new Vector3(0f, a, b));
                v.Add(new Vector3(a, b, 0f));
                v.Add(new Vector3(b, 0f, a));
            }
            return Normalized(v);
        }

        /// <summary>20 vertices of a unit-circumradius dodecahedron: the cube (±1, ±1, ±1) plus cyclic
        /// permutations of (0, ±1/φ, ±φ).</summary>
        public static Vector3[] DodecahedronVertices()
        {
            var v = new List<Vector3>();
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                v.Add(new Vector3(x, y, z));
            foreach (float a in new[] { -1f / Phi, 1f / Phi })
            foreach (float b in new[] { -Phi, Phi })
            {
                v.Add(new Vector3(0f, a, b));
                v.Add(new Vector3(a, b, 0f));
                v.Add(new Vector3(b, 0f, a));
            }
            return Normalized(v);
        }

        /// <summary>Edges of a regular polyhedron: every vertex pair at the minimum distance.</summary>
        public static List<Vector2Int> ShortestEdges(Vector3[] vertices)
        {
            float min = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            for (int j = i + 1; j < vertices.Length; j++)
                min = Mathf.Min(min, Vector3.Distance(vertices[i], vertices[j]));

            var edges = new List<Vector2Int>();
            for (int i = 0; i < vertices.Length; i++)
            for (int j = i + 1; j < vertices.Length; j++)
                if (Vector3.Distance(vertices[i], vertices[j]) <= min * 1.001f)
                    edges.Add(new Vector2Int(i, j));
            return edges;
        }

        /// <summary>Wireframe: a square prism per edge and a small octahedral node per vertex.</summary>
        public static Mesh Wireframe(Vector3[] unitVertices, float radius, float thickness, float nodeSize, string name)
        {
            var builder = new Builder();
            foreach (Vector2Int e in ShortestEdges(unitVertices))
                builder.Prism(unitVertices[e.x] * radius, unitVertices[e.y] * radius, thickness);
            foreach (Vector3 v in unitVertices)
                builder.Octahedron(v * radius, nodeSize);
            return builder.ToMesh(name);
        }

        /// <summary>Regular tetrahedron inscribed in a sphere of the given radius. Inverting it gives
        /// the second half of a stellated octahedron (star tetrahedron).</summary>
        public static Mesh Tetrahedron(float radius, bool inverted, string name)
        {
            float s = (inverted ? -radius : radius) / Mathf.Sqrt(3f);
            Vector3 a = new Vector3(1f, 1f, 1f) * s, b = new Vector3(-1f, -1f, 1f) * s;
            Vector3 c = new Vector3(-1f, 1f, -1f) * s, d = new Vector3(1f, -1f, -1f) * s;
            var builder = new Builder();
            builder.Face(a, c, b, inverted);
            builder.Face(a, b, d, inverted);
            builder.Face(a, d, c, inverted);
            builder.Face(b, c, d, inverted);
            return builder.ToMesh(name);
        }

        public static Mesh Octahedron(float radius, string name)
        {
            var builder = new Builder();
            builder.Octahedron(Vector3.zero, radius);
            return builder.ToMesh(name);
        }

        /// <summary>Sweeps a polygonal cross-section along a closed curve sampled on [0, 2π).
        /// Frames use parallel transport (no twisting from the curve's torsion), and the
        /// residual rotation at the seam is spread evenly along the tube so the ends meet.</summary>
        public static Mesh ClosedTube(Func<float, Vector3> curve, int segments, float tubeRadius, int sides, string name)
        {
            segments = Mathf.Max(3, segments);
            sides = Mathf.Max(3, sides);
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++) points[i] = curve(2f * Mathf.PI * i / segments);

            var tangents = new Vector3[segments];
            for (int i = 0; i < segments; i++)
                tangents[i] = (points[(i + 1) % segments] - points[(i - 1 + segments) % segments]).normalized;

            var normals = new Vector3[segments];
            normals[0] = AnyPerpendicular(tangents[0]);
            for (int i = 1; i < segments; i++)
                normals[i] = Transport(normals[i - 1], tangents[i - 1], tangents[i]);

            // Close the loop: rotate frames progressively so frame n lands on frame 0.
            Vector3 wrapped = Transport(normals[segments - 1], tangents[segments - 1], tangents[0]);
            float seamAngle = Vector3.SignedAngle(wrapped, normals[0], tangents[0]);
            for (int i = 1; i < segments; i++)
                normals[i] = Quaternion.AngleAxis(seamAngle * i / segments, tangents[i]) * normals[i];

            var ring = new Vector3[segments, sides];
            for (int i = 0; i < segments; i++)
            {
                Vector3 binormal = Vector3.Cross(tangents[i], normals[i]);
                for (int j = 0; j < sides; j++)
                {
                    float theta = 2f * Mathf.PI * j / sides;
                    ring[i, j] = points[i] + tubeRadius * (Mathf.Cos(theta) * normals[i] + Mathf.Sin(theta) * binormal);
                }
            }

            var builder = new Builder();
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    builder.Quad(ring[i, j], ring[i, k], ring[n, k], ring[n, j]);
                }
            }
            return builder.ToMesh(name);
        }

        /// <summary>(p, q) torus knot on a torus of major radius 2 and minor radius 1. (2, 3) is the trefoil.</summary>
        public static Vector3 TorusKnot(float t, int p, int q)
        {
            float r = 2f + Mathf.Cos(q * t);
            return new Vector3(r * Mathf.Cos(p * t), r * Mathf.Sin(p * t), Mathf.Sin(q * t));
        }

        private static Vector3 Transport(Vector3 normal, Vector3 fromTangent, Vector3 toTangent)
        {
            Vector3 moved = Quaternion.FromToRotation(fromTangent, toTangent) * normal;
            return Vector3.ProjectOnPlane(moved, toTangent).normalized;
        }

        private static Vector3 AnyPerpendicular(Vector3 v)
        {
            Vector3 other = Mathf.Abs(v.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(v, other).normalized;
        }

        private static Vector3[] Normalized(List<Vector3> v)
        {
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            return v.ToArray();
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            public void Face(Vector3 a, Vector3 b, Vector3 c, bool flip = false)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(flip ? c : b);
                vertices.Add(flip ? b : c);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Face(a, b, c);
                Face(a, c, d);
            }

            public void Prism(Vector3 from, Vector3 to, float thickness)
            {
                Vector3 axis = (to - from).normalized;
                Vector3 u = AnyPerpendicular(axis) * (thickness * 0.5f);
                Vector3 w = Vector3.Cross(axis, u);
                Vector3[] offsets = { u + w, -u + w, -u - w, u - w };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 o0 = offsets[i], o1 = offsets[(i + 1) % 4];
                    Quad(from + o0, from + o1, to + o1, to + o0);
                }
            }

            public void Octahedron(Vector3 center, float radius)
            {
                Vector3[] ring = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = center + ring[i] * radius, b = center + ring[(i + 1) % 4] * radius;
                    Face(center + Vector3.up * radius, a, b);
                    Face(center + Vector3.down * radius, b, a);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

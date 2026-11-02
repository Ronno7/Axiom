using GeometricFlight.Combat;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// Closes a completed rail behind the ship. The moment the hull has fully cleared the passage's end plane, a manual
    /// iris on that plane closes, and a lethal wall in the closed iris's exact outline arms just behind it, so the rail
    /// can't be re-entered and contact always matches what is seen. The wall arms instantly (no ship can turn back
    /// within the iris's close; see the turn-back test), and it is entirely behind the hull, so nothing ever appears
    /// through the ship. Nothing steers the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RailSeal : MonoBehaviour
    {
        [SerializeField] private ArenaFlight flight;
        [Tooltip("The ship's hull box.")]
        [SerializeField] private BoxCollider hull;
        [Tooltip("A manual ClosingIris on the passage's end plane, its blades facing the arena.")]
        [SerializeField] private ClosingIris iris;
        [SerializeField, Min(0.05f)] private float closeSeconds = 0.4f; // Under the quickest turn-back (RailSealTests).
        [Tooltip("Depth of the lethal wall behind the plane, metres.")]
        [SerializeField, Min(0.1f)] private float wallDepth = 1f;

        private Mesh wallMesh;

        public bool Sealed { get; private set; }
        public float EndZ => iris != null ? iris.transform.position.z : transform.position.z;

        private void Update()
        {
            if (Sealed || flight == null || hull == null || !flight.RailReleased) return;
            if (hull.bounds.min.z <= EndZ) return;
            Seal();
        }

        private void OnDestroy()
        {
            if (wallMesh != null) Destroy(wallMesh);
        }

        public void Seal()
        {
            if (Sealed) return;
            Sealed = true;
            if (iris == null) return;
            BuildWall();
            iris.Close(closeSeconds);
        }

        /// <summary>A thin convex prism with the closed iris's hexagon (its outer outline, in world size), starting
        /// just behind the end plane. Marked lethal, like the passage walls.</summary>
        private void BuildWall()
        {
            var wall = new GameObject("Seal wall");
            wall.layer = gameObject.layer;
            wall.transform.SetParent(transform, false);
            wall.transform.SetPositionAndRotation(iris.transform.position, Quaternion.identity);
            float radius = iris.OuterApothem / Mathf.Cos(30f * Mathf.Deg2Rad);
            Vector3 scale = iris.transform.lossyScale;
            var vertices = new Vector3[12];
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i * Mathf.Deg2Rad;
                var corner = new Vector3(Mathf.Cos(angle) * radius * scale.x, Mathf.Sin(angle) * radius * scale.y, 0f);
                vertices[i] = corner + Vector3.back * 0.05f;
                vertices[i + 6] = corner + Vector3.back * (0.05f + wallDepth);
            }
            var triangles = new int[60];
            int t = 0;
            for (int i = 1; i < 5; i++)
            {
                triangles[t++] = 0; triangles[t++] = i; triangles[t++] = i + 1;
                triangles[t++] = 6; triangles[t++] = i + 7; triangles[t++] = i + 6;
            }
            for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6;
                triangles[t++] = i; triangles[t++] = i + 6; triangles[t++] = n + 6;
                triangles[t++] = i; triangles[t++] = n + 6; triangles[t++] = n;
            }
            wallMesh = new Mesh { name = "Seal wall", vertices = vertices, triangles = triangles };
            wall.AddComponent<LethalSurface>();
            var collider = wall.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = wallMesh;
        }
    }
}

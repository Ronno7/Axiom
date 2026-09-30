using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>
    /// Timed obstacle that rewards speed. When the ship crosses the trigger line (TriggerDistance
    /// before the iris), six blades start closing a hexagonal aperture while twisting like a camera
    /// iris. As the ship passes the iris plane, its hull must fit inside the aperture, otherwise
    /// the shield takes a hit (the ship still flies through, keeping failure forgiving).
    /// At cruise the aperture is shut on arrival; at high throttle it is still open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClosingIris : MonoBehaviour
    {
        [SerializeField] private PlayerFlight ship;
        [SerializeField] private ShipShield shield;
        [SerializeField] private BoxCollider shipHull;
        [SerializeField] private Material bladeMaterialA;
        [SerializeField] private Material bladeMaterialB;
        [SerializeField] private Material rimMaterial;
        [Tooltip("The iris starts closing when the ship is this far before it (Z).")]
        [SerializeField, Min(1f)] private float triggerDistance = 32f;
        [SerializeField, Min(0.1f)] private float closeDuration = 5.2f;
        [Tooltip("Aperture size (center to flat side) when fully open.")]
        [SerializeField, Min(0.5f)] private float openApothem = 8.5f;
        [SerializeField, Min(1f)] private float outerApothem = 11f;
        [Tooltip("Blade twist over a full close, degrees.")]
        [SerializeField] private float twist = 50f;

        public float TriggerZ => transform.position.z - triggerDistance;
        public float Apothem { get; private set; }
        public float Rotation { get; private set; }
        public bool Closing { get; private set; }

        private Mesh bladeMesh, rimMesh;
        private float elapsed;
        private float previousShipZ;

        private void Awake()
        {
            Apothem = openApothem;
            bladeMesh = CreateChild("Blades", new[] { bladeMaterialA, bladeMaterialB }, 0f);
            rimMesh = CreateChild("Rim", new[] { rimMaterial }, -0.03f);
            previousShipZ = ship != null ? ship.transform.position.z : float.NegativeInfinity;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (bladeMesh != null) Destroy(bladeMesh);
            if (rimMesh != null) Destroy(rimMesh);
        }

        private void Update()
        {
            if (ship == null || Time.deltaTime <= 0f) return;
            float shipZ = ship.transform.position.z;
            if (!Closing && shipZ >= TriggerZ) Closing = true;
            if (Closing && Apothem > 0f)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / closeDuration);
                Apothem = openApothem * (1f - progress);
                Rotation = twist * progress;
                Rebuild();
            }

            float irisZ = transform.position.z;
            if (previousShipZ < irisZ && shipZ >= irisZ && ship.isActiveAndEnabled)
            {
                Vector3 hullCenter = ship.transform.TransformPoint(shipHull.center) - transform.position;
                Vector2 halfExtents = new Vector2(shipHull.size.x, shipHull.size.y) * 0.5f;
                if (!RectFitsAperture(new Vector2(hullCenter.x, hullCenter.y), halfExtents, Apothem, Rotation))
                    shield.ApplyHit();
            }
            previousShipZ = shipZ;
        }

        /// <summary>True if an axis-aligned rectangle lies inside a regular hexagon centered at the
        /// origin with the given apothem, rotated by rotationDegrees (0 = flat top and bottom).
        /// The hexagon is the intersection of three slabs |p·n| ≤ apothem, n at 30°, 90°, 150°.</summary>
        public static bool RectFitsAperture(Vector2 center, Vector2 halfExtents, float apothem, float rotationDegrees)
        {
            if (apothem <= 0f) return false;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 p = center + new Vector2(corner % 2 == 0 ? -halfExtents.x : halfExtents.x,
                    corner < 2 ? -halfExtents.y : halfExtents.y);
                for (int side = 0; side < 3; side++)
                {
                    float angle = (30f + 60f * side + rotationDegrees) * Mathf.Deg2Rad;
                    if (Mathf.Abs(p.x * Mathf.Cos(angle) + p.y * Mathf.Sin(angle)) > apothem) return false;
                }
            }
            return true;
        }

        private Mesh CreateChild(string childName, Material[] materials, float zOffset)
        {
            var go = new GameObject(childName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, zOffset);
            var mesh = new Mesh { name = "Iris " + childName };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mesh;
        }

        /// <summary>Blade i spans aperture vertices i..i+1 (rotated) to outer vertices i..i+1 (fixed),
        /// so the blades shear as the iris twists. Alternating submeshes make the blades readable.</summary>
        private void Rebuild()
        {
            float cos30 = Mathf.Cos(30f * Mathf.Deg2Rad);
            float innerRadius = Apothem / cos30, outerRadius = outerApothem / cos30;
            float rimWidth = 0.18f;
            var inner = new Vector3[6];
            var rimOuter = new Vector3[6];
            var outer = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (60f * i + Rotation) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                inner[i] = dir * innerRadius;
                rimOuter[i] = dir * (innerRadius + rimWidth / cos30);
                float b = 60f * i * Mathf.Deg2Rad;
                outer[i] = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * outerRadius;
            }

            var vertices = new Vector3[24];
            var even = new int[18];
            var odd = new int[18];
            for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6, v = i * 4;
                vertices[v] = inner[i];
                vertices[v + 1] = inner[n];
                vertices[v + 2] = outer[n];
                vertices[v + 3] = outer[i];
                int[] target = i % 2 == 0 ? even : odd;
                int t = (i / 2) * 6;
                // Faces toward -Z (the approaching ship): clockwise as seen from behind.
                target[t] = v; target[t + 1] = v + 2; target[t + 2] = v + 3;
                target[t + 3] = v; target[t + 4] = v + 1; target[t + 5] = v + 2;
            }
            bladeMesh.Clear();
            bladeMesh.subMeshCount = 2;
            bladeMesh.vertices = vertices;
            bladeMesh.SetTriangles(even, 0);
            bladeMesh.SetTriangles(odd, 1);
            bladeMesh.RecalculateNormals();
            bladeMesh.RecalculateBounds();

            var rimVertices = new Vector3[12];
            var rimTriangles = new int[36];
            for (int i = 0; i < 6; i++)
            {
                rimVertices[i * 2] = inner[i];
                rimVertices[i * 2 + 1] = rimOuter[i];
            }
            for (int i = 0; i < 6; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = (i + 1) % 6 * 2, b1 = b0 + 1, t = i * 6;
                rimTriangles[t] = a0; rimTriangles[t + 1] = b1; rimTriangles[t + 2] = a1;
                rimTriangles[t + 3] = a0; rimTriangles[t + 4] = b0; rimTriangles[t + 5] = b1;
            }
            rimMesh.Clear();
            rimMesh.vertices = rimVertices;
            rimMesh.triangles = rimTriangles;
            rimMesh.RecalculateNormals();
            rimMesh.RecalculateBounds();
        }
    }
}

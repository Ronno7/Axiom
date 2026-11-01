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
    [DefaultExecutionOrder(10)] // Sample the crossing after flight moves, before LevelFlow checks failure.
    [DisallowMultipleComponent]
    public sealed class ClosingIris : MonoBehaviour
    {
        [SerializeField] private ShipFlight ship;
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
        /// <summary>The first forward crossing since reset has been evaluated.</summary>
        public bool Crossed { get; private set; }
        /// <summary>Whether that first crossing cleared the aperture; false before any crossing.</summary>
        public bool Passed { get; private set; }

        private Mesh bladeMesh, rimMesh;
        private float elapsed;
        private Vector3 previousShipPosition;
        private Vector3 previousHullPosition, previousHullScale;
        private Quaternion previousHullRotation;

        private void Awake()
        {
            bladeMesh = CreateChild("Blades", new[] { bladeMaterialA, bladeMaterialB }, 0f);
            rimMesh = CreateChild("Rim", new[] { rimMaterial }, -0.03f);
            ResetForStudy();
        }

        private void OnDestroy()
        {
            if (bladeMesh != null) Destroy(bladeMesh);
            if (rimMesh != null) Destroy(rimMesh);
        }

        private void Update()
        {
            if (ship == null || Time.deltaTime <= 0f) return;
            Matrix4x4 worldToIris = transform.worldToLocalMatrix;
            // Trigger distance remains world metres even when the reused iris geometry is scaled.
            float previousZ = Vector3.Dot(previousShipPosition - transform.position, transform.forward);
            float shipZ = Vector3.Dot(ship.transform.position - transform.position, transform.forward);
            float previousElapsed = elapsed;
            float activeFrom = 0f;
            if (!Closing && shipZ >= -triggerDistance)
            {
                Closing = true;
                if (previousZ < -triggerDistance)
                    activeFrom = Mathf.InverseLerp(previousZ, shipZ, -triggerDistance);
            }
            if (Closing)
            {
                elapsed += Time.deltaTime * (1f - activeFrom);
                float progress = Mathf.Clamp01(elapsed / closeDuration);
                Apothem = openApothem * (1f - progress);
                Rotation = twist * progress;
                if (previousElapsed < closeDuration) Rebuild();
            }

            if (TryForwardCrossing(previousZ, shipZ, out float crossing) && ship.isActiveAndEnabled)
            {
                Transform hullTransform = shipHull.transform;
                var hullToWorld = Matrix4x4.TRS(
                    Vector3.Lerp(previousHullPosition, hullTransform.position, crossing),
                    Quaternion.Slerp(previousHullRotation, hullTransform.rotation, crossing),
                    Vector3.Lerp(previousHullScale, hullTransform.lossyScale, crossing));
                float crossingElapsed = previousElapsed + Time.deltaTime * Mathf.Max(0f, crossing - activeFrom);
                float progress = Mathf.Clamp01(crossingElapsed / closeDuration);
                bool fits = BoxFitsAperture(worldToIris * hullToWorld, shipHull.center, shipHull.size * 0.5f,
                    openApothem * (1f - progress), twist * progress);
                if (!Crossed)
                {
                    Crossed = true;
                    Passed = fits;
                }
                if (!fits) shield.ApplyHit();
            }
            RememberPose();
        }

        public float TriggerDistance => triggerDistance;

        /// <summary>Seconds after the trigger within which a level ship on the centre line still fits through
        /// (the same fit test as a real crossing). Courses use it to set which speeds pass.</summary>
        public float ArrivalWindow()
        {
            if (shipHull == null) return closeDuration;
            Matrix4x4 hullToIris = transform.worldToLocalMatrix
                * Matrix4x4.TRS(transform.position, transform.rotation, shipHull.transform.lossyScale);
            float fits = openApothem, fails = 0f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (fits + fails) * 0.5f;
                if (BoxFitsAperture(hullToIris, shipHull.center, shipHull.size * 0.5f, mid, twist * (1f - mid / openApothem)))
                    fits = mid;
                else fails = mid;
            }
            return closeDuration * (1f - fits / openApothem);
        }

        /// <summary>Restores the existing iris for an explicit study restart. It never moves the ship.</summary>
        public void ResetForStudy()
        {
            elapsed = 0f;
            Closing = false;
            Crossed = false;
            Passed = false;
            Apothem = openApothem;
            Rotation = 0f;
            RememberPose();
            if (bladeMesh != null && rimMesh != null) Rebuild();
        }

        private void RememberPose()
        {
            if (ship != null) previousShipPosition = ship.transform.position;
            if (shipHull == null) return;
            previousHullPosition = shipHull.transform.position;
            previousHullRotation = shipHull.transform.rotation;
            previousHullScale = shipHull.transform.lossyScale;
        }

        /// <summary>Fraction of a forward segment at the iris plane. Starting on/past the plane is
        /// not a fresh crossing; reversed travel does not retrigger the obstacle.</summary>
        public static bool TryForwardCrossing(float previousZ, float currentZ, out float fraction)
        {
            fraction = 0f;
            if (previousZ >= 0f || currentZ < 0f) return false;
            fraction = -previousZ / (currentZ - previousZ);
            return true;
        }

        /// <summary>Projects all eight corners of the oriented hull into the iris's local plane.
        /// Hull and iris scale are included in hullToIris; pitch/roll cannot hide the hull's depth.</summary>
        public static bool BoxFitsAperture(Matrix4x4 hullToIris, Vector3 center, Vector3 halfExtents,
            float apothem, float rotationDegrees)
        {
            if (apothem <= 0f) return false;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = center + new Vector3((corner & 1) == 0 ? -halfExtents.x : halfExtents.x,
                    (corner & 2) == 0 ? -halfExtents.y : halfExtents.y,
                    (corner & 4) == 0 ? -halfExtents.z : halfExtents.z);
                Vector3 point = hullToIris.MultiplyPoint3x4(local);
                for (int side = 0; side < 3; side++)
                {
                    float angle = (30f + 60f * side + rotationDegrees) * Mathf.Deg2Rad;
                    if (Mathf.Abs(point.x * Mathf.Cos(angle) + point.y * Mathf.Sin(angle)) > apothem)
                        return false;
                }
            }
            return true;
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

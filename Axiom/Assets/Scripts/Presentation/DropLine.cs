using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>A thin line from this object straight down to the floor with a ring where it lands, so an
    /// object's height and ground position read at a glance in open 3D space (the radar "stalks" of space
    /// games). Hides with the object.</summary>
    [DisallowMultipleComponent]
    public sealed class DropLine : MonoBehaviour
    {
        [SerializeField] private Material material;
        [Tooltip("World height of the floor.")]
        [SerializeField] private float floorHeight;
        [SerializeField, Min(0f)] private float width = 0.08f;
        [SerializeField, Min(0.1f)] private float ringRadius = 1.6f;

        private LineRenderer stalk, ring;

        private void Awake()
        {
            if (material == null)
            {
                enabled = false;
                return;
            }
            stalk = Line("Stalk", 2, false);
            ring = Line("Foot", 24, true);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24;
                ring.SetPosition(i, new Vector3(Mathf.Sin(a) * ringRadius, 0f, Mathf.Cos(a) * ringRadius));
            }
        }

        private void OnEnable() => SetVisible(true);
        private void OnDisable() => SetVisible(false);

        private void OnDestroy()
        {
            if (stalk != null) Destroy(stalk.gameObject);
            if (ring != null) Destroy(ring.gameObject);
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            var foot = new Vector3(p.x, floorHeight + 0.05f, p.z);
            stalk.SetPosition(0, p);
            stalk.SetPosition(1, foot);
            ring.transform.position = foot;
        }

        private LineRenderer Line(string label, int points, bool loop)
        {
            var go = new GameObject(name + " " + label);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.positionCount = points;
            line.loop = loop;
            line.useWorldSpace = !loop;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private void SetVisible(bool visible)
        {
            if (stalk != null) stalk.gameObject.SetActive(visible);
            if (ring != null) ring.gameObject.SetActive(visible);
        }
    }
}

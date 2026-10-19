using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>Physical throttle cue: two faceted jets rooted in the rear engine ports.
    /// Reads the existing input snapshot; never polls or changes the flight input.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ThrustDisplay : MonoBehaviour
    {
        [SerializeField] private PlayerFlight flight;
        [SerializeField] private Vector3 leftPort = new Vector3(-0.85f, 0f, -1.64f);
        [SerializeField] private Vector3 rightPort = new Vector3(0.85f, 0f, -1.64f);
        [Tooltip("Jet length at minimum and maximum throttle, in aircraft-local units.")]
        [SerializeField] private Vector2 lengthRange = new Vector2(0.45f, 2.6f);
        [SerializeField, Min(0.1f)] private float response = 12f;

        // Two jets, each with an outer diamond, a core and three travelling diamonds.
        private readonly Vector3[] vertices = new Vector3[240];
        private readonly Color[] colors = new Color[240];
        private Mesh mesh;
        private MeshRenderer jetRenderer;
        private float throttle, phase;
        private int cursor;

        private void OnEnable()
        {
            if (flight == null) flight = GetComponentInParent<PlayerFlight>();
            jetRenderer = GetComponent<MeshRenderer>();
            jetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            jetRenderer.receiveShadows = false;
            mesh = new Mesh { name = "Geometric engine exhaust", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            throttle = flight != null ? Mathf.InverseLerp(flight.MinSpeed, flight.MaxSpeed, flight.CruiseSpeed) : 0.44f;
            phase = 0f;
            DrawJets();
            var indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.triangles = indices;
        }

        private void LateUpdate()
        {
            bool running = !Application.isPlaying || (flight != null && flight.isActiveAndEnabled);
            jetRenderer.enabled = running;
            if (!running) return;
            if (Application.isPlaying)
            {
                // Scaled time freezes both response and motion while paused.
                if (Time.deltaTime <= 0f) return;
                float target = Mathf.InverseLerp(flight.MinSpeed, flight.MaxSpeed, flight.TargetSpeed);
                throttle = Mathf.Lerp(throttle, target, 1f - Mathf.Exp(-response * Time.deltaTime));
                phase = Mathf.Repeat(phase + Time.deltaTime * Mathf.Lerp(1.4f, 3.2f, throttle), 1f);
            }
            DrawJets();
        }

        private void DrawJets()
        {
            cursor = 0;
            DrawJet(leftPort, 0f);
            DrawJet(rightPort, 0.17f);
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private void DrawJet(Vector3 port, float offset)
        {
            float pulse = Mathf.Sin((phase + offset) * Mathf.PI * 2f);
            float length = Mathf.Lerp(lengthRange.x, lengthRange.y, throttle) * (1f + pulse * 0.035f);
            float width = Mathf.Lerp(0.12f, 0.22f, throttle);
            Diamond(port, length, width, 0.16f, new Color(0.72f, 0.78f, 0.82f, 0.3f));
            Diamond(port, length * 0.72f, width * 0.56f, 0.18f, new Color(1f, 1f, 1f, 0.95f));
            for (int i = 0; i < 3; i++)
            {
                float travel = Mathf.Repeat(phase + offset + i / 3f, 1f);
                float fade = Mathf.Sin(travel * Mathf.PI);
                Vector3 origin = port + Vector3.back * length * (0.6f + travel * 0.85f);
                float size = Mathf.Lerp(0.025f, 0.09f, throttle) * fade;
                Diamond(origin, size * 4f, size, 0.5f,
                    new Color(0.9f, 0.94f, 0.96f, fade * Mathf.Lerp(0.12f, 0.65f, throttle)));
            }
        }

        private void Diamond(Vector3 origin, float length, float width, float waist, Color color)
        {
            Vector3 middle = origin + Vector3.back * length * waist;
            Vector3 end = origin + Vector3.back * length;
            for (int side = 0; side < 4; side++)
            {
                float a = side * Mathf.PI * 0.5f, b = (side + 1) * Mathf.PI * 0.5f;
                Vector3 p = middle + new Vector3(Mathf.Cos(a) * width, Mathf.Sin(a) * width * 0.7f, 0f);
                Vector3 q = middle + new Vector3(Mathf.Cos(b) * width, Mathf.Sin(b) * width * 0.7f, 0f);
                Color facet = color * (side % 2 == 0 ? 1f : 0.78f);
                Triangle(origin, p, q, facet);
                Triangle(q, p, end, facet);
            }
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            vertices[cursor] = a; colors[cursor++] = color;
            vertices[cursor] = b; colors[cursor++] = color;
            vertices[cursor] = c; colors[cursor++] = color;
        }

        private void OnDisable()
        {
            if (jetRenderer != null) jetRenderer.enabled = false;
            if (mesh == null) return;
            GetComponent<MeshFilter>().sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}

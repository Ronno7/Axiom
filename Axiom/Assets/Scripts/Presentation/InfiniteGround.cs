using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// One endless ground: a large plane that follows the camera across the menu, passage and arena.
    /// Its grid is drawn in world space by the material, so the lines stay fixed in the world while the
    /// plane moves with the view, and the far edge dissolves into fog. Visual only: collision belongs to
    /// the surfaces that need it. Nothing generated here is saved.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [SelectionBase] // Clicking a generated (hidden) part selects this object.
    public sealed class InfiniteGround : MonoBehaviour
    {
        [Tooltip("The camera the ground stays under. Empty uses the main camera.")]
        [SerializeField] private Camera view;
        [SerializeField] private Material material;
        [Tooltip("Ground height in world metres.")]
        [SerializeField] private float height;
        [Tooltip("Plane size, in metres. Fog should hide its edge.")]
        [SerializeField, Min(10f)] private float size = 4000f;
        [Tooltip("In Play Mode the grid slides toward the camera at this speed (m/s), as if gliding over the ground. "
               + "0 = still. A sequence can change it through GlideSpeed.")]
        [SerializeField, Min(0f)] private float glideSpeed;

        private static readonly int GridOffset = Shader.PropertyToID("_GridOffset");
        private GameObject plane;
        private Mesh mesh;
        private MaterialPropertyBlock block;
        private float glided;

        /// <summary>Metres per second the grid slides toward the camera. The menu glides; a flight sets it to 0
        /// as the camera starts to move, so the motion carries on without a hitch.</summary>
        public float GlideSpeed
        {
            get => glideSpeed;
            set => glideSpeed = Mathf.Max(0f, value);
        }

        private void OnEnable() => Build();

        private void OnDisable()
        {
            Generated.Release(plane);
            Generated.Release(mesh);
            plane = null;
            mesh = null;
        }

        private void OnValidate()
        {
            if (plane != null) plane.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void LateUpdate()
        {
            Camera target = view != null ? view : Camera.main;
            if (plane == null || target == null) return;
            Vector3 eye = target.transform.position;
            plane.transform.position = new Vector3(eye.x, height, eye.z);
            if (!Application.isPlaying || glideSpeed <= 0f) return;
            // Lines sit where (world + offset) meets the grid, so a growing offset moves them toward the camera.
            // Wrap on a whole number of major cells so the lines never jump.
            float wrap = material != null && material.HasProperty("_MajorSpacing")
                ? Mathf.Max(1f, material.GetFloat("_MajorSpacing")) * 10f : 1000f;
            glided = Mathf.Repeat(glided + glideSpeed * Time.deltaTime, wrap);
            block ??= new MaterialPropertyBlock();
            block.SetVector(GridOffset, new Vector4(0f, glided, 0f, 0f));
            plane.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        private void Build()
        {
            OnDisable();
            float h = size * 0.5f;
            mesh = new Mesh
            {
                name = "Infinite ground",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            // Shown (greyed, never saved) so a Scene view click can select it; [SelectionBase] then selects the owner.
            plane = new GameObject("Infinite ground (generated)") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            plane.layer = gameObject.layer;
            plane.transform.SetParent(transform, false);
            plane.transform.position = new Vector3(0f, height, 0f);
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = plane.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

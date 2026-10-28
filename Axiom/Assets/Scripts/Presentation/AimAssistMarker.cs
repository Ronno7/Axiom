using GeometricFlight.Combat;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Shows what aim assist is on: four corner brackets around the target the next shot will go
    /// through, facing the camera at a constant size on screen. Hidden when nothing is near the firing line.</summary>
    [DisallowMultipleComponent]
    public sealed class AimAssistMarker : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private Material material;
        [Tooltip("Bracket size as a share of the distance to the target (constant on screen).")]
        [SerializeField, Min(0.001f)] private float size = 0.03f;
        [Tooltip("Line width as a share of the distance to the target.")]
        [SerializeField, Min(0.0001f)] private float width = 0.0012f;
        [Tooltip("Never smaller than this, metres, so close targets are still framed.")]
        [SerializeField, Min(0f)] private float minimumSize = 4f;

        private readonly LineRenderer[] corners = new LineRenderer[4];
        private Camera view;

        private void Awake()
        {
            view = Camera.main;
            if (weapon == null || material == null)
            {
                enabled = false;
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("AssistBracket_" + i);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.positionCount = 3;
                line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.numCornerVertices = 0;
                corners[i] = line;
            }
        }

        private void LateUpdate()
        {
            ShootableTarget target = weapon.AssistTarget;
            bool show = target != null && view != null;
            foreach (LineRenderer c in corners) c.enabled = show;
            if (!show) return;

            Vector3 centre = target.transform.position;
            Transform cam = view.transform;
            float distance = Vector3.Distance(cam.position, centre);
            float half = Mathf.Max(minimumSize, distance * size);
            float leg = half * 0.45f;
            Vector3 right = cam.right, up = cam.up;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
                Vector3 corner = centre + (right * sx + up * sy) * half;
                LineRenderer line = corners[i];
                line.widthMultiplier = distance * width;
                line.SetPosition(0, corner - up * sy * leg);
                line.SetPosition(1, corner);
                line.SetPosition(2, corner - right * sx * leg);
            }
        }
    }
}

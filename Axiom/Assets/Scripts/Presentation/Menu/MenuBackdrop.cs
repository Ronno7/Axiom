using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Menu attract scene motion: floor markers scroll toward the camera and wrap, like
    /// the course seen in flight. Constructs stay where they are placed in the scene.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float scrollSpeed = 4f;
        [SerializeField] private Transform[] floorMarkers;
        [SerializeField, Min(1f)] private float markerSpacing = 12f;
        private bool presentationMotion;

        /// <summary>A moving-camera study uses stationary, repeatable floor markings, so camera
        /// travel does not add a second independent floor speed. Also tolerates large view transfers.</summary>
        public void SetPresentationMotion(bool active, float cameraZ = 0f)
        {
            presentationMotion = active;
            if (!active || floorMarkers.Length == 0) return;
            float nearest = cameraZ - markerSpacing;
            float loop = markerSpacing * floorMarkers.Length;
            for (int i = 0; i < floorMarkers.Length; i++)
            {
                Vector3 position = floorMarkers[i].position;
                position.z = nearest + Mathf.Repeat(4f + i * markerSpacing - nearest, loop);
                floorMarkers[i].position = position;
            }
        }

        private void Update()
        {
            if (presentationMotion) return;
            float step = scrollSpeed * Time.deltaTime;
            float loop = markerSpacing * floorMarkers.Length;
            float nearest = viewCamera.transform.position.z - markerSpacing;
            foreach (Transform marker in floorMarkers)
            {
                Vector3 p = marker.position;
                p.z -= step;
                if (p.z < nearest) p.z += loop;
                marker.position = p;
            }
        }
    }
}

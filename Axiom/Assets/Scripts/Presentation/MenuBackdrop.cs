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

        private void Update()
        {
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

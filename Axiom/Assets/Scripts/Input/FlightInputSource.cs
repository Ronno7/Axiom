using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>Device-independent steering: X is right, Y is up, magnitude is at most one.</summary>
    public abstract class FlightInputSource : MonoBehaviour
    {
        public abstract Vector2 ReadSteering();
    }
}

using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Stateless movement rules for the current straight, world +Z course.</summary>
    public static class FlightMovement
    {
        public static Vector3 Step(Vector3 position, Vector2 steering, float deltaTime,
            float forwardSpeed, float steeringSpeed, Rect bounds)
        {
            if (deltaTime <= 0f) return position;

            // Preserve analog magnitude while preventing faster diagonal movement.
            Vector2 displacement = Vector2.ClampMagnitude(steering, 1f)
                * (Mathf.Max(0f, steeringSpeed) * deltaTime);
            return new Vector3(
                Mathf.Clamp(position.x + displacement.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(position.y + displacement.y, bounds.yMin, bounds.yMax),
                position.z + Mathf.Max(0f, forwardSpeed) * deltaTime);
        }
    }
}

using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Stateless movement rules for the current straight, world +Z course.</summary>
    public static class FlightMovement
    {
        public static float TargetSpeed(float throttle, float cruiseSpeed, Vector2 multipliers)
        {
            float minimum = Mathf.Clamp(multipliers.x, 0.01f, 1f);
            float maximum = Mathf.Max(1f, multipliers.y);
            float multiplier = throttle < 0f
                ? Mathf.Lerp(1f, minimum, -throttle) : Mathf.Lerp(1f, maximum, throttle);
            return Mathf.Max(0.01f, cruiseSpeed) * multiplier;
        }

        /// <summary>Inverse of TargetSpeed: the throttle (-1 to 1) that asks for this speed.</summary>
        public static float ThrottleForSpeed(float speed, float cruiseSpeed, Vector2 multipliers)
        {
            float minimum = Mathf.Clamp(multipliers.x, 0.01f, 1f);
            float maximum = Mathf.Max(1f, multipliers.y);
            float multiplier = speed / Mathf.Max(0.01f, cruiseSpeed);
            if (multiplier < 1f) return minimum < 1f ? -Mathf.Clamp01((1f - multiplier) / (1f - minimum)) : 0f;
            return maximum > 1f ? Mathf.Clamp01((multiplier - 1f) / (maximum - 1f)) : 0f;
        }

        /// <summary>Integrates a constant-acceleration ramp, including any time at target speed.
        /// Returns the frame's average speed so long frames do not overestimate travel.</summary>
        public static float Accelerate(ref float speed, float target, float acceleration, float deltaTime)
        {
            if (deltaTime <= 0f) return speed;
            float previous = speed;
            float rate = Mathf.Max(0.01f, acceleration);
            float rampTime = Mathf.Min(deltaTime, Mathf.Abs(target - previous) / rate);
            speed = Mathf.MoveTowards(previous, target, rate * deltaTime);
            return ((previous + speed) * 0.5f * rampTime + speed * (deltaTime - rampTime)) / deltaTime;
        }

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

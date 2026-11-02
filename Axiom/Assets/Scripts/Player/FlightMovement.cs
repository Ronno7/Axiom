using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Stateless speed rules shared by the flight models.</summary>
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
    }
}

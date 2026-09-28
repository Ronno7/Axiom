using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>One device-independent input snapshot. Throttle is a signed setpoint:
    /// -1 is minimum, 0 is cruise, +1 is maximum. Pause is a press edge; fire is held.</summary>
    public readonly struct FlightInput
    {
        public readonly Vector2 Steering;
        public readonly float Throttle;
        public readonly bool FireHeld;
        public readonly bool PausePressed;

        public FlightInput(Vector2 steering, float throttle, bool fireHeld, bool pausePressed)
        {
            Steering = Vector2.ClampMagnitude(steering, 1f);
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            FireHeld = fireHeld;
            PausePressed = pausePressed;
        }
    }

    /// <summary>Read once per frame. Delta time allows a keyboard to adjust a latched
    /// throttle; an absolute hardware adapter can supply its calibrated setpoint directly.</summary>
    public abstract class FlightInputSource : MonoBehaviour
    {
        public abstract FlightInput ReadInput(float deltaTime);
    }
}

using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>One device-independent input snapshot. Throttle is a signed setpoint:
    /// -1 is minimum, 0 is cruise, +1 is maximum. Twist is the stick's rotation axis, -1 (anticlockwise)
    /// to +1 (clockwise). Pause is a press edge; fire is held.</summary>
    public readonly struct FlightInput
    {
        public readonly Vector2 Steering;
        public readonly float Throttle;
        public readonly bool FireHeld;
        public readonly bool PausePressed;
        public readonly float Twist;

        public FlightInput(Vector2 steering, float throttle, bool fireHeld, bool pausePressed, float twist = 0f)
        {
            Twist = Mathf.Clamp(twist, -1f, 1f);
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

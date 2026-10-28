using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>The ship's input: keyboard always works; the Arduino controller joins in when connected.
    /// PlayerFlight reads this source, and it reads the keyboard exactly once per frame.</summary>
    [DisallowMultipleComponent]
    public sealed class CombinedFlightInput : FlightInputSource
    {
        [SerializeField] private KeyboardFlightInput keyboard;

        /// <summary>True while a controller is connected (HUD prompts adapt to it).</summary>
        public bool UsingController { get; private set; }

        public override FlightInput ReadInput(float deltaTime)
        {
            FlightInput keys = keyboard != null && keyboard.isActiveAndEnabled ? keyboard.ReadInput(deltaTime) : default;
            ControllerState controller = ControllerService.Instance.State;
            UsingController = controller.Connected;
            return FlightInputMerge.Merge(keys, controller);
        }
    }

    public static class FlightInputMerge
    {
        /// <summary>Without a controller, keyboard input passes through. With one: its stick (and twist)
        /// steers unless a steering (or twist) key is held, its sonar sets the throttle (easing to cruise if
        /// the sensor is lost), and fire or pause come from either device.</summary>
        public static FlightInput Merge(FlightInput keyboard, ControllerState controller)
        {
            if (!controller.Connected) return keyboard;
            Vector2 steering = keyboard.Steering.sqrMagnitude > 0.0001f ? keyboard.Steering : controller.Stick;
            float twist = Mathf.Abs(keyboard.Twist) > 0.0001f ? keyboard.Twist : controller.Twist;
            return new FlightInput(steering, controller.Throttle,
                keyboard.FireHeld || controller.Fire, keyboard.PausePressed || controller.StickButtonPressed, twist);
        }
    }
}

using GeometricFlight.Core;
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
        /// <summary>Explicit keyboard takeover, used by the opening prototype's paused recovery screen.</summary>
        public bool KeyboardOnly { get; set; }

        public override FlightInput ReadInput(float deltaTime)
        {
            FlightInput keys = keyboard != null && keyboard.isActiveAndEnabled ? keyboard.ReadInput(deltaTime) : default;
            ControllerState controller = ControllerService.Instance.State;
            if (KeyboardOnly) controller = default;
            UsingController = controller.Connected;
            return FlightInputMerge.Merge(keys, controller, GameSettings.KeyboardThrottleWithController);
        }

        /// <summary>Only the keyboard's latched throttle can be moved; a connected lever stays where it is.</summary>
        public override bool LimitThrottle(float setpoint) =>
            keyboard != null && (!UsingController || GameSettings.KeyboardThrottleWithController)
            && keyboard.LimitThrottle(setpoint);
    }

    public static class FlightInputMerge
    {
        /// <summary>Without a controller, keyboard input passes through. With one: its stick (and twist)
        /// steers unless a steering (or twist) key is held. The selected throttle source supplies its
        /// setpoint, and weapons/pause come from either device. Hardware throttle retains its filtered
        /// ease to cruise on sensor loss; it never silently switches to a stale keyboard setpoint.</summary>
        public static FlightInput Merge(FlightInput keyboard, ControllerState controller,
            bool keyboardThrottleWithController = false)
        {
            if (!controller.Connected)
                return new FlightInput(keyboard.Steering, keyboard.Throttle, keyboard.FireHeld,
                    keyboard.PausePressed, keyboard.Twist, keyboard.LockHeld,
                    keyboard.LockCancelled || controller.LockCancelled, keyboard.EmpHeld);
            Vector2 steering = keyboard.Steering.sqrMagnitude > 0.0001f ? keyboard.Steering : controller.Stick;
            float twist = Mathf.Abs(keyboard.Twist) > 0.0001f ? keyboard.Twist : controller.Twist;
            float throttle = keyboardThrottleWithController ? keyboard.Throttle : controller.Throttle;
            return new FlightInput(steering, throttle,
                keyboard.FireHeld || controller.Fire, keyboard.PausePressed || controller.StickButtonPressed,
                twist, keyboard.LockHeld || controller.LockHeld,
                keyboard.LockCancelled || controller.LockCancelled, keyboard.EmpHeld || controller.EmpHeld);
        }
    }
}

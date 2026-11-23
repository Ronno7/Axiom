using GeometricFlight.Core;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>The ship's input: the keyboard and a gamepad always work; the Arduino controller joins in when
    /// connected. The flight reads this source, and it reads each device exactly once per frame.</summary>
    [DisallowMultipleComponent]
    public sealed class CombinedFlightInput : FlightInputSource
    {
        [SerializeField] private KeyboardFlightInput keyboard;
        [Tooltip("An Xbox-style gamepad, flown alongside the keyboard.")]
        [SerializeField] private GamepadFlightInput gamepad;

        /// <summary>True while a controller is connected (HUD prompts adapt to it).</summary>
        public bool UsingController { get; private set; }

        /// <summary>The gamepad, rather than the keyboard, was the last one used; prompts name its buttons.</summary>
        public static bool GamepadInUse { get; private set; }

        public override FlightInput ReadInput(float deltaTime)
        {
            FlightInput keys = keyboard != null && keyboard.isActiveAndEnabled ? keyboard.ReadInput(deltaTime) : default;
            FlightInput pad = gamepad != null && gamepad.isActiveAndEnabled ? gamepad.ReadInput(deltaTime) : default;
            if (gamepad != null && gamepad.Actuated) GamepadInUse = true;
            else if (keyboard != null && keyboard.Actuated) GamepadInUse = false;
            ControllerState controller = ControllerService.Instance.State;
            UsingController = controller.Connected;
            FlightInput local = FlightInputMerge.Local(keys, pad, gamepad != null && gamepad.ThrottleHeld);
            return FlightInputMerge.Merge(local, controller, GameSettings.KeyboardThrottleWithController);
        }

        /// <summary>Only the keyboard's latched throttle can be moved; a connected lever or held triggers stay where
        /// they are.</summary>
        public override bool LimitThrottle(float setpoint) =>
            keyboard != null && (gamepad == null || !gamepad.ThrottleHeld)
            && (!UsingController || GameSettings.KeyboardThrottleWithController)
            && keyboard.LimitThrottle(setpoint);
    }

    public static class FlightInputMerge
    {
        /// <summary>The keyboard and a gamepad together: the keyboard steers (and twists) while its keys are held,
        /// otherwise the pad does; held triggers own the throttle, otherwise the keyboard's latched setting holds; and
        /// weapons and pause come from either.</summary>
        public static FlightInput Local(FlightInput keyboard, FlightInput pad, bool padThrottle)
        {
            Vector2 steering = keyboard.Steering.sqrMagnitude > 0.0001f ? keyboard.Steering : pad.Steering;
            float twist = Mathf.Abs(keyboard.Twist) > 0.0001f ? keyboard.Twist : pad.Twist;
            return new FlightInput(steering, padThrottle ? pad.Throttle : keyboard.Throttle,
                keyboard.FireHeld || pad.FireHeld, keyboard.PausePressed || pad.PausePressed, twist,
                keyboard.LockHeld || pad.LockHeld, keyboard.LockCancelled || pad.LockCancelled,
                keyboard.EmpHeld || pad.EmpHeld);
        }

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
                keyboard.FireHeld || controller.Fire, keyboard.PausePressed || controller.MenuPressed,
                twist, keyboard.LockHeld || controller.LockHeld,
                keyboard.LockCancelled || controller.LockCancelled, keyboard.EmpHeld || controller.EmpHeld);
        }
    }
}

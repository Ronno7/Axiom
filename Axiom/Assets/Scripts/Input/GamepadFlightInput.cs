using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
    /// <summary>
    /// An Xbox-style gamepad. The left stick steers like the cockpit stick and the right stick twists. The triggers are
    /// an absolute throttle: the right trigger pushes toward full and, squeezed all the way, into the boost gate; the
    /// left trigger pulls toward minimum; released, the ship cruises. Bumpers fire and paint missiles, Y releases the
    /// EMP, View is MASTER ARM (<see cref="MasterArmInput"/>) and Menu pauses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamepadFlightInput : FlightInputSource
    {
        [SerializeField] private string steerBinding = "<Gamepad>/leftStick";
        [SerializeField] private string twistBinding = "<Gamepad>/rightStick/x";
        [SerializeField] private string raiseBinding = "<Gamepad>/rightTrigger";
        [SerializeField] private string lowerBinding = "<Gamepad>/leftTrigger";
        [SerializeField] private string fireBinding = "<Gamepad>/rightShoulder";
        [SerializeField] private string lockBinding = "<Gamepad>/leftShoulder";
        [SerializeField] private string empBinding = "<Gamepad>/buttonNorth";
        [SerializeField] private string pauseBinding = "<Gamepad>/start";
        [Tooltip("Below this a trigger counts as released, so a resting finger doesn't move the throttle.")]
        [SerializeField, Range(0f, 0.3f)] private float triggerRest = 0.05f;

        private InputAction steer, twist, raise, lower, fire, missileLock, emp, pause;
        private bool lockWasHeld;

        /// <summary>True when this frame's read had a stick, trigger or button in use: the pad is the one being flown.</summary>
        public bool Actuated { get; private set; }
        /// <summary>True while either trigger is held: the pad owns the throttle.</summary>
        public bool ThrottleHeld { get; private set; }

        private void Awake()
        {
            steer = new InputAction("Pad Steering", InputActionType.Value, steerBinding);
            twist = new InputAction("Pad Twist", InputActionType.Value, twistBinding);
            raise = new InputAction("Pad Throttle Up", InputActionType.Value, raiseBinding);
            lower = new InputAction("Pad Throttle Down", InputActionType.Value, lowerBinding);
            fire = new InputAction("Pad Fire", InputActionType.Button, fireBinding);
            missileLock = new InputAction("Pad Missile Lock", InputActionType.Button, lockBinding);
            emp = new InputAction("Pad EMP", InputActionType.Button, empBinding);
            pause = new InputAction("Pad Pause", InputActionType.Button, pauseBinding);
        }

        private InputAction[] All => new[] { steer, twist, raise, lower, fire, missileLock, emp, pause };

        private void OnEnable() { foreach (InputAction action in All) action?.Enable(); }
        private void OnDisable() { foreach (InputAction action in All) action?.Disable(); }
        private void OnDestroy() { foreach (InputAction action in All) action?.Dispose(); }

        public override FlightInput ReadInput(float deltaTime)
        {
            Actuated = ThrottleHeld = false;
            if (!isActiveAndEnabled || steer == null || Gamepad.current == null)
            {
                // A pad lost mid-paint cancels the volley; losing it is not a release that launches.
                bool cancelled = lockWasHeld;
                lockWasHeld = false;
                return new FlightInput(Vector2.zero, 0f, false, false, lockCancelled: cancelled);
            }
            float up = raise.ReadValue<float>(), down = lower.ReadValue<float>();
            ThrottleHeld = up > triggerRest || down > triggerRest;
            var input = new FlightInput(steer.ReadValue<Vector2>(), Throttle(up, down, triggerRest), fire.IsPressed(),
                pause.WasPressedThisFrame(), twist.ReadValue<float>(), missileLock.IsPressed(), empHeld: emp.IsPressed());
            lockWasHeld = input.LockHeld;
            Actuated = ThrottleHeld || input.Steering.sqrMagnitude > 0f || Mathf.Abs(input.Twist) > 0f || input.FireHeld
                || input.LockHeld || input.EmpHeld || input.PausePressed;
            return input;
        }

        /// <summary>The throttle setpoint from the two triggers: cruise at rest, the right trigger toward full (and the
        /// boost gate at the end of its travel), the left toward minimum.</summary>
        public static float Throttle(float rightTrigger, float leftTrigger, float rest = 0.05f)
        {
            float up = Rest(rightTrigger, rest), down = Rest(leftTrigger, rest);
            return Mathf.Clamp(up - down, -1f, 1f);
        }

        private static float Rest(float trigger, float rest) =>
            trigger <= rest ? 0f : Mathf.Clamp01((trigger - rest) / Mathf.Max(0.0001f, 1f - rest));
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
    /// <summary>
    /// An Xbox-style gamepad in the jet layout: the left stick pitches and rolls like the cockpit stick, the bumpers
    /// twist (yaw), and the triggers move the latched throttle the keyboard keeps (<see cref="ThrottleChange"/>): the
    /// right trigger raises it, held at full into the boost gate, the left lowers it, and released it stays put, so the
    /// fingers are free for the bumpers. A fires the guns, B paints missiles, Y releases the EMP, View is MASTER ARM
    /// (<see cref="MasterArmInput"/>) and Menu pauses. The right stick is left free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamepadFlightInput : FlightInputSource
    {
        private const string SteerBinding = "<Gamepad>/leftStick";
        private const string TwistLeftBinding = "<Gamepad>/leftShoulder";
        private const string TwistRightBinding = "<Gamepad>/rightShoulder";
        private const string RaiseBinding = "<Gamepad>/rightTrigger";
        private const string LowerBinding = "<Gamepad>/leftTrigger";
        private const string FireBinding = "<Gamepad>/buttonSouth";
        private const string LockBinding = "<Gamepad>/buttonEast";
        private const string EmpBinding = "<Gamepad>/buttonNorth";
        private const string PauseBinding = "<Gamepad>/start";
        [Tooltip("Below this a trigger counts as released, so a resting finger doesn't move the throttle.")]
        [SerializeField, Range(0f, 0.3f)] private float triggerRest = 0.05f;

        private InputAction steer, twist, raise, lower, fire, missileLock, emp, pause;
        private bool lockWasHeld;

        /// <summary>True when this frame's read had a stick, trigger or button in use: the pad is the one being flown.</summary>
        public bool Actuated { get; private set; }
        /// <summary>How the triggers move the latched throttle this frame (-1…+1, like Q/E): the pad's snapshot carries
        /// no setpoint of its own.</summary>
        public float ThrottleChange { get; private set; }

        private void Awake()
        {
            steer = new InputAction("Pad Steering", InputActionType.Value, SteerBinding);
            twist = new InputAction("Pad Twist", InputActionType.Value);
            twist.AddCompositeBinding("1DAxis").With("Negative", TwistLeftBinding).With("Positive", TwistRightBinding);
            raise = new InputAction("Pad Throttle Up", InputActionType.Value, RaiseBinding);
            lower = new InputAction("Pad Throttle Down", InputActionType.Value, LowerBinding);
            fire = new InputAction("Pad Fire", InputActionType.Button, FireBinding);
            missileLock = new InputAction("Pad Missile Lock", InputActionType.Button, LockBinding);
            emp = new InputAction("Pad EMP", InputActionType.Button, EmpBinding);
            pause = new InputAction("Pad Pause", InputActionType.Button, PauseBinding);
        }

        private InputAction[] All => new[] { steer, twist, raise, lower, fire, missileLock, emp, pause };

        private void OnEnable() { foreach (InputAction action in All) action?.Enable(); }
        private void OnDisable() { foreach (InputAction action in All) action?.Disable(); }
        private void OnDestroy() { foreach (InputAction action in All) action?.Dispose(); }

        public override FlightInput ReadInput(float deltaTime)
        {
            Actuated = false;
            ThrottleChange = 0f;
            if (!isActiveAndEnabled || steer == null || Gamepad.current == null)
            {
                // A pad lost mid-paint cancels the volley; losing it is not a release that launches.
                bool cancelled = lockWasHeld;
                lockWasHeld = false;
                return new FlightInput(Vector2.zero, 0f, false, false, lockCancelled: cancelled);
            }
            ThrottleChange = Change(raise.ReadValue<float>(), lower.ReadValue<float>(), triggerRest);
            var input = new FlightInput(steer.ReadValue<Vector2>(), 0f, fire.IsPressed(),
                pause.WasPressedThisFrame(), twist.ReadValue<float>(), missileLock.IsPressed(), empHeld: emp.IsPressed());
            lockWasHeld = input.LockHeld;
            Actuated = ThrottleChange != 0f || input.Steering.sqrMagnitude > 0f || Mathf.Abs(input.Twist) > 0f
                || input.FireHeld || input.LockHeld || input.EmpHeld || input.PausePressed;
            return input;
        }

        /// <summary>The throttle change from the two triggers: nothing at rest, the right trigger up and the left down,
        /// faster the further they're squeezed.</summary>
        public static float Change(float rightTrigger, float leftTrigger, float rest = 0.05f)
        {
            float up = Rest(rightTrigger, rest), down = Rest(leftTrigger, rest);
            return Mathf.Clamp(up - down, -1f, 1f);
        }

        private static float Rest(float trigger, float rest) =>
            trigger <= rest ? 0f : Mathf.Clamp01((trigger - rest) / Mathf.Max(0.0001f, 1f - rest));
    }
}

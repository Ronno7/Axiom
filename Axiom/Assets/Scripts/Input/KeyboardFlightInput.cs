using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
    /// <summary>
    /// The keyboard, and the latched throttle: Q and E (and a gamepad's triggers, passed in through
    /// <see cref="AddThrottleChange"/>) move the setting at a steady rate, and it stays where they leave it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyboardFlightInput : FlightInputSource
    {
        [SerializeField, Min(0.01f)] private float throttleChangeRate = 1f;
        [SerializeField] private string lowerThrottleBinding = "<Keyboard>/q";
        [SerializeField] private string raiseThrottleBinding = "<Keyboard>/e";
        [SerializeField] private string fireBinding = "<Keyboard>/space";
        [SerializeField] private string lockBinding = "<Keyboard>/leftShift";
        [SerializeField] private string pauseBinding = "<Keyboard>/escape";
        [SerializeField] private string twistLeftBinding = "<Keyboard>/z";
        [SerializeField] private string twistRightBinding = "<Keyboard>/c";
        private InputAction steering;
        private InputAction twist;
        private InputAction throttle;
        private InputAction fire;
        private InputAction missileLock;
        private InputAction emp;
        private InputAction pause;
        private float throttleSetting;
        private float pendingThrottleChange;

        /// <summary>True when this frame's read had a key in use: the keyboard is the one being flown.</summary>
        public bool Actuated { get; private set; }

        /// <summary>Moves the latched throttle in the next read as Q/E would (-1…+1, scaled by the rate): a gamepad's
        /// triggers share the latch, so switching between keys and pad never makes the throttle jump.</summary>
        public void AddThrottleChange(float change) => pendingThrottleChange += change;

        /// <summary>The latch: the setting moves by change × rate × time and holds when change is zero.</summary>
        public static float Latch(float setting, float change, float rate, float deltaTime) =>
            Mathf.Clamp(setting + Mathf.Clamp(change, -1f, 1f) * Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime),
                -1f, 1f);

        private void Awake()
        {
            steering = new InputAction("Flight Steering", InputActionType.Value);
            steering.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            steering.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            throttle = new InputAction("Flight Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis")
                .With("Negative", lowerThrottleBinding)
                .With("Positive", raiseThrottleBinding);
            twist = new InputAction("Flight Twist", InputActionType.Value);
            twist.AddCompositeBinding("1DAxis")
                .With("Negative", twistLeftBinding)
                .With("Positive", twistRightBinding);
            fire = new InputAction("Flight Fire", InputActionType.Button, fireBinding);
            missileLock = new InputAction("Missile Lock", InputActionType.Button, lockBinding);
            emp = new InputAction("EMP", InputActionType.Button, "<Keyboard>/r");
            pause = new InputAction("Flight Pause", InputActionType.Button, pauseBinding);
        }

        private void OnEnable()
        {
            throttleSetting = 0f;
            steering.Enable();
            throttle.Enable();
            twist.Enable();
            fire.Enable();
            missileLock.Enable();
            emp.Enable();
            pause.Enable();
        }

        private void OnDisable()
        {
            steering?.Disable();
            throttle?.Disable();
            twist?.Disable();
            fire?.Disable();
            missileLock?.Disable();
            emp?.Disable();
            pause?.Disable();
        }

        private void OnDestroy()
        {
            steering?.Dispose();
            throttle?.Dispose();
            twist?.Dispose();
            fire?.Dispose();
            missileLock?.Dispose();
            emp?.Dispose();
            pause?.Dispose();
        }

        public override bool LimitThrottle(float setpoint)
        {
            throttleSetting = Mathf.Min(throttleSetting, setpoint);
            return true;
        }

        public override FlightInput ReadInput(float deltaTime)
        {
            Actuated = false;
            float padChange = pendingThrottleChange;
            pendingThrottleChange = 0f;
            if (!isActiveAndEnabled || steering == null) return default;
            float change = throttle.ReadValue<float>();
            throttleSetting = Latch(throttleSetting, change + padChange, throttleChangeRate, deltaTime);
            var input = new FlightInput(steering.ReadValue<Vector2>(), throttleSetting,
                fire.IsPressed(), pause.WasPressedThisFrame(), twist.ReadValue<float>(), missileLock.IsPressed(),
                empHeld: emp.IsPressed());
            Actuated = change != 0f || input.Steering.sqrMagnitude > 0f || input.Twist != 0f || input.FireHeld
                || input.LockHeld || input.EmpHeld || input.PausePressed;
            return input;
        }
    }
}

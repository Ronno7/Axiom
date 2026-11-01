using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
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
            if (!isActiveAndEnabled || steering == null) return default;
            throttleSetting = Mathf.Clamp(throttleSetting + throttle.ReadValue<float>()
                * Mathf.Max(0f, throttleChangeRate) * Mathf.Max(0f, deltaTime), -1f, 1f);
            return new FlightInput(steering.ReadValue<Vector2>(), throttleSetting,
                fire.IsPressed(), pause.WasPressedThisFrame(), twist.ReadValue<float>(), missileLock.IsPressed(),
                empHeld: emp.IsPressed());
        }
    }
}

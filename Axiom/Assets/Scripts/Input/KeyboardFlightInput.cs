using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
    [DisallowMultipleComponent]
    public sealed class KeyboardFlightInput : FlightInputSource
    {
        private InputAction steering;

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
        }

        private void OnEnable() => steering.Enable();
        private void OnDisable() => steering?.Disable();
        private void OnDestroy() => steering?.Dispose();

        public override Vector2 ReadSteering() =>
            isActiveAndEnabled && steering != null ? steering.ReadValue<Vector2>() : Vector2.zero;
    }
}

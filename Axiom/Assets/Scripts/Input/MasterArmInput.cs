using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
    /// <summary>Opt-in input adapter for commissioning. Existing scenes keep their original input path.</summary>
    [DefaultExecutionOrder(-25), DisallowMultipleComponent]
    public sealed class MasterArmInput : FlightInputSource
    {
        [SerializeField] private CombinedFlightInput source;
        private InputAction toggle;
        private readonly WeaponAuthority authority = new WeaponAuthority();
        private bool keyboardArmed;
        private bool previousArmed;
        private bool previousPhysical;
        public bool WeaponsAvailable { get; set; }
        public bool HasPhysicalSwitch => !source.KeyboardOnly && ControllerService.Instance.State.HasMasterArm;
        public bool Armed => HasPhysicalSwitch ? ControllerService.Instance.State.Armed : keyboardArmed;
        public bool KeyboardOnly => source.KeyboardOnly;
        public FlightInput RawInput { get; private set; }
        public bool RefusedThisFrame => authority.RefusedThisFrame;

        private void Awake() => toggle = new InputAction("Master Arm", InputActionType.Button, "<Keyboard>/t");
        private void OnEnable() => toggle.Enable();
        private void OnDisable() { toggle.Disable(); authority.RequireRelease(); }
        private void OnDestroy() => toggle.Dispose();

        private void Update()
        {
            // Preserve the last accepted switch position while the recovery screen selects a device.
            if (previousPhysical && !HasPhysicalSwitch) keyboardArmed = previousArmed;
            if (!HasPhysicalSwitch && toggle.WasPressedThisFrame()) keyboardArmed = !keyboardArmed;
            if (previousArmed != Armed || previousPhysical != HasPhysicalSwitch) authority.RequireRelease();
            previousArmed = Armed;
            previousPhysical = HasPhysicalSwitch;
        }

        public void PrepareRun(bool tutorial)
        {
            keyboardArmed = !tutorial;
            WeaponsAvailable = false;
            authority.RequireRelease();
        }

        public void SelectKeyboard()
        {
            keyboardArmed = previousArmed;
            source.KeyboardOnly = true;
            authority.RequireRelease();
        }

        public void SelectController()
        {
            source.KeyboardOnly = false;
            authority.RequireRelease();
        }

        public void RequireRelease() => authority.RequireRelease();

        public override bool LimitThrottle(float setpoint) => source != null && source.LimitThrottle(setpoint);

        public override FlightInput ReadInput(float deltaTime)
        {
            RawInput = source.ReadInput(deltaTime);
            return authority.Filter(RawInput, Armed, WeaponsAvailable && deltaTime > 0f);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Input
{
    /// <summary>Weapons authority: the MASTER ARM switch while a cockpit is connected, T on the keyboard otherwise.</summary>
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
        public bool HasPhysicalSwitch => ControllerService.Current != null && ControllerService.Current.State.Connected;
        public bool Armed => HasPhysicalSwitch ? ControllerService.Current.State.Armed : keyboardArmed;
        public FlightInput RawInput { get; private set; }
        public bool RefusedThisFrame => authority.RefusedThisFrame;

        private void Awake() => toggle = new InputAction("Master Arm", InputActionType.Button, "<Keyboard>/t");
        private void OnEnable() => toggle.Enable();
        private void OnDisable() { toggle.Disable(); authority.RequireRelease(); }
        private void OnDestroy() => toggle.Dispose();

        private void Update()
        {
            // Unplugging keeps the last switch position until T changes it.
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

        public void RequireRelease() => authority.RequireRelease();

        public override bool LimitThrottle(float setpoint) => source != null && source.LimitThrottle(setpoint);

        public override FlightInput ReadInput(float deltaTime)
        {
            RawInput = source.ReadInput(deltaTime);
            return authority.Filter(RawInput, Armed, WeaponsAvailable && deltaTime > 0f);
        }
    }
}

using GeometricFlight.Input;

namespace GeometricFlight.Core
{
    /// <summary>Names the right control in a guidance line for keyboard or cockpit: {fire}, {arm}, {lower}, {raise},
    /// {boost}, {lock}, {emp}. Each control comes out in brackets ("[LEFT SHIFT]"), which the guidance panel draws as a
    /// key chip.</summary>
    public static class TutorialCaptions
    {
        public static string Resolve(string line, MasterArmInput authority)
        {
            if (string.IsNullOrEmpty(line)) return line;
            ControllerState device = ControllerService.Instance.State;
            bool keyboard = !device.Connected;
            bool keyThrottle = keyboard || GameSettings.KeyboardThrottleWithController;
            return line.Replace("{fire}", keyboard ? "[SPACE]" : "[FIRE]")
                .Replace("{lower}", keyThrottle ? "[Q]" : "THROTTLE BACK")
                .Replace("{raise}", keyThrottle ? "[E]" : "THROTTLE FORWARD")
                .Replace("{boost}", keyThrottle ? "HOLD [E]" : "TO THE FINAL STOP")
                .Replace("{arm}", authority != null && authority.HasPhysicalSwitch ? "[MASTER ARM]" : "[T]")
                .Replace("{lock}", !keyboard && device.HasLock ? "[LOCK]" : "[LEFT SHIFT]")
                .Replace("{emp}", !keyboard && device.HasMasterArm ? "[EMP]" : "[R]");
        }
    }
}

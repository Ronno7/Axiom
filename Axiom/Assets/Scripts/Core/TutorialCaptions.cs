using GeometricFlight.Input;

namespace GeometricFlight.Core
{
    /// <summary>Names the right control in a tutorial caption for keyboard or cockpit: {fire}, {arm}, {lower},
    /// {raise}, {boost}, {lock}, {emp}. With chips, each control comes out in brackets ("[LEFT SHIFT]") so the
    /// guidance card draws it as a key chip.</summary>
    public static class TutorialCaptions
    {
        public static string Resolve(string caption, MasterArmInput authority, bool chips = false)
        {
            if (string.IsNullOrEmpty(caption)) return caption;
            ControllerState device = ControllerService.Instance.State;
            bool keyboard = !device.Connected;
            bool keyThrottle = keyboard || GameSettings.KeyboardThrottleWithController;
            string Control(string name) => chips ? "[" + name + "]" : name;
            return caption.Replace("{fire}", Control(keyboard ? "SPACE" : "FIRE"))
                .Replace("{lower}", keyThrottle ? Control("Q") : "THROTTLE BACK")
                .Replace("{raise}", keyThrottle ? Control("E") : "THROTTLE FORWARD")
                .Replace("{boost}", keyThrottle ? "HOLD " + Control("E") : "TO THE FINAL STOP")
                .Replace("{arm}", Control(authority != null && authority.HasPhysicalSwitch ? "MASTER ARM" : "T"))
                .Replace("{lock}", Control(!keyboard && device.HasLock ? "LOCK" : "LEFT SHIFT"))
                .Replace("{emp}", Control(!keyboard && device.HasMasterArm ? "EMP" : "R"));
        }
    }
}

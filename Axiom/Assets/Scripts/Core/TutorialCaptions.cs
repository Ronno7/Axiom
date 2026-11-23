using GeometricFlight.Input;

namespace GeometricFlight.Core
{
    /// <summary>Names the right control in a guidance line for the keyboard, a gamepad or the cockpit: {fire}, {arm},
    /// {lower}, {raise}, {boost}, {lock}, {emp}. Each control comes out in brackets ("[LEFT SHIFT]"), which the guidance
    /// panel draws as a key chip.</summary>
    public static class TutorialCaptions
    {
        public enum Controls { Keyboard, Gamepad, Cockpit }

        /// <summary>What the player is flying with: the cockpit while connected, else the keyboard or pad last used.</summary>
        public static Controls Current => ControllerService.Instance.State.Connected ? Controls.Cockpit
            : CombinedFlightInput.GamepadInUse ? Controls.Gamepad : Controls.Keyboard;

        public static string Resolve(string line) =>
            Resolve(line, Current, GameSettings.KeyboardThrottleWithController);

        public static string Resolve(string line, Controls controls, bool keyboardThrottleWithCockpit)
        {
            if (string.IsNullOrEmpty(line)) return line;
            bool pad = controls == Controls.Gamepad, keys = controls == Controls.Keyboard;
            bool keyThrottle = keys || (controls == Controls.Cockpit && keyboardThrottleWithCockpit);
            return line.Replace("{fire}", pad ? "[RB]" : keys ? "[SPACE]" : "[FIRE]")
                .Replace("{lower}", pad ? "[LT]" : keyThrottle ? "[Q]" : "THROTTLE BACK")
                .Replace("{raise}", pad ? "[RT]" : keyThrottle ? "[E]" : "THROTTLE FORWARD")
                .Replace("{boost}", pad ? "[RT] ALL THE WAY" : keyThrottle ? "HOLD [E]" : "TO THE FINAL STOP")
                .Replace("{arm}", pad ? "[VIEW]" : keys ? "[T]" : "[MASTER ARM]")
                .Replace("{lock}", pad ? "[LB]" : keys ? "[LEFT SHIFT]" : "[LOCK]")
                .Replace("{emp}", pad ? "[Y]" : keys ? "[R]" : "[EMP]");
        }
    }
}

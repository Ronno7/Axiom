namespace GeometricFlight.Input
{
    /// <summary>Weapons inhibit without altering flight. A change of authority cannot turn a held
    /// action into a new shot or turn cancelled LOCK into a release volley.</summary>
    public sealed class WeaponAuthority
    {
        private bool fireRelease = true, lockRelease = true, empRelease = true;
        private bool previouslyAllowed;
        private bool previousRequest;
        public bool RefusedThisFrame { get; private set; }

        public void RequireRelease()
        {
            fireRelease = lockRelease = empRelease = true;
            previouslyAllowed = false;
        }

        public FlightInput Filter(FlightInput raw, bool armed, bool available)
        {
            bool allowed = armed && available;
            if (!allowed || allowed != previouslyAllowed) RequireRelease();
            // Each channel must be released independently; releasing FIRE must not unblock held EMP.
            if (!raw.FireHeld) fireRelease = false;
            if (!raw.LockHeld) lockRelease = false;
            if (!raw.EmpHeld) empRelease = false;
            bool request = raw.FireHeld || raw.LockHeld || raw.EmpHeld;
            RefusedThisFrame = available && !armed && request && !previousRequest;
            bool cancel = raw.LockCancelled || !allowed || !previouslyAllowed || lockRelease;
            previousRequest = request;
            previouslyAllowed = allowed;
            return new FlightInput(raw.Steering, raw.Throttle, allowed && !fireRelease && raw.FireHeld,
                raw.PausePressed, raw.Twist, allowed && !lockRelease && raw.LockHeld, cancel,
                allowed && !empRelease && raw.EmpHeld);
        }
    }
}

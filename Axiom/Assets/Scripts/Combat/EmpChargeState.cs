namespace GeometricFlight.Combat
{
    /// <summary>One deliberate press per life. Pause, focus loss and reset require a fresh release.</summary>
    public sealed class EmpChargeState
    {
        private bool requireRelease = true;
        public bool Ready { get; private set; } = true;

        public bool Step(bool held, bool allowed)
        {
            if (!allowed) { Cancel(); return false; }
            if (!held) { requireRelease = false; return false; }
            if (requireRelease || !Ready) return false;
            requireRelease = true;
            Ready = false;
            return true;
        }

        public void Cancel() => requireRelease = true;
        public void Reset() { Ready = true; Cancel(); }
    }
}

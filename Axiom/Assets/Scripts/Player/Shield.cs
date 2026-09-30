using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Segmented shield rules. Each accepted hit removes one segment and starts a short
    /// protection window during which further hits are ignored. Depletes exactly once.</summary>
    public sealed class Shield
    {
        public int Max { get; }
        public int Current { get; private set; }
        public float ProtectionRemaining { get; private set; }
        public bool IsDepleted => Current == 0;
        public bool IsProtected => ProtectionRemaining > 0f;

        private readonly float protectionDuration;

        public Shield(int segments, float protectionSeconds)
        {
            Max = Mathf.Max(1, segments);
            Current = Max;
            protectionDuration = Mathf.Max(0f, protectionSeconds);
        }

        /// <summary>Returns true if the hit removed a segment.</summary>
        public bool TryHit()
        {
            if (IsDepleted || IsProtected) return false;
            Current--;
            ProtectionRemaining = IsDepleted ? 0f : protectionDuration;
            return true;
        }

        /// <summary>Advance protection time. Zero or negative time (paused) changes nothing.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) ProtectionRemaining = Mathf.Max(0f, ProtectionRemaining - deltaTime);
        }
    }
}

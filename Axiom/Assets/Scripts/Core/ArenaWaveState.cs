using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Core
{
    public enum ArenaWavePhase { Inactive, Incoming, Combat, Cleared, Defeated }

    /// <summary>A repeatable encounter with a visible warning, a finite enemy count and a recovery break.
    /// Only registered kills during combat count; duplicate and late reports cannot advance a round.</summary>
    public sealed class ArenaWaveState
    {
        private readonly int enemyCount;
        private readonly float warningTime, recoveryTime;
        private readonly HashSet<int> defeated = new HashSet<int>();
        public ArenaWavePhase Phase { get; private set; }
        public int Round { get; private set; }
        public int Total => enemyCount;
        public int Remaining => enemyCount - defeated.Count;
        public float Elapsed { get; private set; }
        public float TimeLeft => Phase == ArenaWavePhase.Incoming ? Mathf.Max(0f, warningTime - Elapsed)
            : Phase == ArenaWavePhase.Cleared ? Mathf.Max(0f, recoveryTime - Elapsed) : 0f;
        public float WarningProgress => Mathf.Clamp01(Elapsed / warningTime);

        public ArenaWaveState(int enemyCount, float warningTime, float recoveryTime)
        {
            this.enemyCount = Mathf.Max(1, enemyCount);
            this.warningTime = Mathf.Max(0.1f, warningTime);
            this.recoveryTime = Mathf.Max(0.1f, recoveryTime);
        }

        public void Restart()
        {
            if (Round == 0) Round = 1;
            defeated.Clear();
            Enter(ArenaWavePhase.Incoming);
        }

        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f || Phase == ArenaWavePhase.Inactive || Phase == ArenaWavePhase.Defeated) return;
            Elapsed += deltaTime;
            if (Phase == ArenaWavePhase.Incoming && Elapsed >= warningTime) Enter(ArenaWavePhase.Combat);
            else if (Phase == ArenaWavePhase.Cleared && Elapsed >= recoveryTime)
            {
                Round++;
                Restart();
            }
        }

        public bool ReportDestroyed(int slot)
        {
            if (Phase != ArenaWavePhase.Combat || slot < 0 || slot >= enemyCount || !defeated.Add(slot)) return false;
            if (Remaining == 0) Enter(ArenaWavePhase.Cleared);
            return true;
        }

        public void Defeat() => Enter(ArenaWavePhase.Defeated);
        public void Stop() => Enter(ArenaWavePhase.Inactive);

        private void Enter(ArenaWavePhase phase)
        {
            Phase = phase;
            Elapsed = 0f;
        }
    }
}

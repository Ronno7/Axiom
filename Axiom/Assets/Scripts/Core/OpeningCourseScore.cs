using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>One award per gate/target. Used by the opening prototype and the rail studies.</summary>
    public sealed class OpeningCourseScore
    {
        private readonly HashSet<int> passed = new HashSet<int>();
        private readonly HashSet<int> destroyed = new HashSet<int>();
        public int Score { get; private set; }
        public int GatesHit { get; private set; }
        public int GatesPassed => passed.Count;
        public void Reset() { passed.Clear(); destroyed.Clear(); Score = GatesHit = 0; }
        public bool CrossGate(int id, Vector2 offset, float radius)
        {
            if (!passed.Add(id)) return false;
            if (offset.sqrMagnitude > radius * radius) return false;
            GatesHit++;
            Score += 100;
            return true;
        }
        public void DestroyTarget(int id) { if (destroyed.Add(id)) Score += 50; }

        /// <summary>The score at a checkpoint, so a retry from it can't award the same gate twice or keep
        /// awards earned after it.</summary>
        public readonly struct Snapshot
        {
            internal readonly int[] Passed, Destroyed;
            internal readonly int Score, GatesHit;
            internal Snapshot(HashSet<int> passed, HashSet<int> destroyed, int score, int gatesHit)
            {
                Passed = new int[passed.Count]; passed.CopyTo(Passed);
                Destroyed = new int[destroyed.Count]; destroyed.CopyTo(Destroyed);
                Score = score; GatesHit = gatesHit;
            }
        }

        public Snapshot Save() => new Snapshot(passed, destroyed, Score, GatesHit);

        public void Restore(Snapshot s)
        {
            passed.Clear(); destroyed.Clear();
            if (s.Passed != null) passed.UnionWith(s.Passed);
            if (s.Destroyed != null) destroyed.UnionWith(s.Destroyed);
            Score = s.Score; GatesHit = s.GatesHit;
        }
    }
}

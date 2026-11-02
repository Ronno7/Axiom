using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>One award per ring. A ring passed outside its circle is spent, so it can't be claimed later.</summary>
    public sealed class RingScore
    {
        private readonly HashSet<int> passed = new HashSet<int>();

        public int Hit { get; private set; }

        /// <summary>The ship crossed this ring's plane this far from its centre. Returns whether that scored.</summary>
        public bool Cross(int ring, Vector2 offset, float radius)
        {
            if (!passed.Add(ring) || offset.sqrMagnitude > radius * radius) return false;
            Hit++;
            return true;
        }

        /// <summary>The score at a checkpoint, so a retry from it can't award the same ring twice or keep
        /// awards earned after it.</summary>
        public readonly struct Snapshot
        {
            internal readonly int[] Passed;
            internal readonly int Hit;
            internal Snapshot(HashSet<int> passed, int hit)
            {
                Passed = new int[passed.Count];
                passed.CopyTo(Passed);
                Hit = hit;
            }
        }

        public Snapshot Save() => new Snapshot(passed, Hit);

        public void Restore(Snapshot snapshot)
        {
            passed.Clear();
            if (snapshot.Passed != null) passed.UnionWith(snapshot.Passed);
            Hit = snapshot.Hit;
        }
    }
}

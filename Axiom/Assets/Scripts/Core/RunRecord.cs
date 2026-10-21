namespace GeometricFlight.Core
{
    /// <summary>A run's final numbers, captured once at the moment it ends.</summary>
    public readonly struct RunResults
    {
        public readonly bool Completed;
        public readonly float Time;
        public readonly int TargetsDestroyed, TargetsTotal, ShotsFired, Hits, ShieldRemaining, ShieldMax;

        public RunResults(bool completed, float time, int targetsDestroyed, int targetsTotal, int shotsFired, int hits,
            int shieldRemaining, int shieldMax)
        {
            Completed = completed;
            Time = time;
            TargetsDestroyed = targetsDestroyed;
            TargetsTotal = targetsTotal;
            ShotsFired = shotsFired;
            Hits = hits;
            ShieldRemaining = shieldRemaining;
            ShieldMax = shieldMax;
        }

        /// <summary>Hits per shot fired, 0 to 1; 0 when nothing was fired.</summary>
        public float Accuracy => ShotsFired == 0 ? 0f : (float)Hits / ShotsFired;
    }

    /// <summary>
    /// The scoring policy for one run: the first ending (complete or failed) is final, and nothing counts after
    /// it. Shots already in flight may still shatter a target, and a late enemy shot may still reach the shield,
    /// but neither changes the result, so the results screen and the cockpit LCD always show the same numbers.
    /// </summary>
    public sealed class RunRecord
    {
        public int TargetsDestroyed { get; private set; }
        public bool Finished { get; private set; }
        public RunResults Results { get; private set; }

        /// <summary>Counts a destroyed target; ignored once the run has ended.</summary>
        public void CountTarget()
        {
            if (!Finished) TargetsDestroyed++;
        }

        /// <summary>Ends the run with these results. Returns false (and keeps the first results) if it already ended.</summary>
        public bool TryFinish(in RunResults results)
        {
            if (Finished) return false;
            Finished = true;
            Results = results;
            return true;
        }
    }
}

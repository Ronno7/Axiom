using System.Collections.Generic;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// Pacing check for a rail: flies the real nose-led rail flight model through a list of gates at one fixed
    /// throttle setting with a simple pilot (Bank &amp; Turn, aiming at the next gate), and reports which gates it
    /// makes. A slow section should fail at full throttle and pass slowly; a fast one the other way round. The pilot
    /// is a reasonable player, not a perfect one, so treat results as a guide for layout, not a guarantee.
    /// </summary>
    public static class RailCourseCheck
    {
        /// <summary>An opening to fly through, centred at <see cref="Center"/>: a circle of <see cref="Radius"/>
        /// (a ring), or when <see cref="HalfWidth"/> is above zero, a slot that wide at any height (a wall gap).</summary>
        public readonly struct Gate
        {
            public readonly Vector3 Center;
            public readonly float Radius, HalfWidth;
            /// <summary>A timed gate (a closing iris) must be reached within <see cref="Window"/> seconds of the
            /// ship crossing <see cref="TriggerDistance"/> before it. Zero = untimed.</summary>
            public readonly float TriggerDistance, Window;
            public readonly string Name;

            public Gate(string name, Vector3 center, float radius, float halfWidth = 0f, float triggerDistance = 0f,
                float window = 0f)
            {
                Name = name;
                Center = center;
                Radius = radius;
                HalfWidth = halfWidth;
                TriggerDistance = triggerDistance;
                Window = window;
            }

            public bool Timed => Window > 0f;

            public bool Contains(Vector2 offset) => HalfWidth > 0f
                ? Mathf.Abs(offset.x) <= HalfWidth
                : offset.sqrMagnitude <= Radius * Radius;
        }

        public readonly struct Result
        {
            public readonly int Passed, Total;
            /// <summary>Index of the first gate missed, or -1.</summary>
            public readonly int FirstMiss;
            /// <summary>The passage boundary was hit (lethal) before the end.</summary>
            public readonly bool Crashed;
            /// <summary>Per gate, in Z order: made or missed.</summary>
            public readonly bool[] Made;

            public Result(int passed, int total, int firstMiss, bool crashed, bool[] made)
            {
                Passed = passed;
                Total = total;
                FirstMiss = firstMiss;
                Crashed = crashed;
                Made = made;
            }
        }

        /// <param name="start">Where the ship starts, flying straight down +Z at the throttle's speed.</param>
        /// <param name="bounds">Passage half-width and height; leaving it is a crash.</param>
        public static Result Fly(IReadOnlyList<Gate> gates, float throttle, ArenaFlightTuning tuning,
            float headingLimit, Vector3 start, Vector2 bounds, float step = 1f / 60f)
        {
            var sorted = new List<Gate>(gates);
            sorted.Sort((a, b) => a.Center.z.CompareTo(b.Center.z));
            var state = new ArenaFlightState
            {
                Position = start,
                Speed = FlightMovement.TargetSpeed(ThrottleBoost.Lever(throttle, tuning.boost), tuning.cruiseSpeed,
                    tuning.throttleMultipliers),
            };
            int next = 0, passed = 0, firstMiss = -1;
            var made = new bool[sorted.Count];
            var triggeredAt = new float[sorted.Count];
            for (int g = 0; g < triggeredAt.Length; g++) triggeredAt[g] = -1f;
            float time = 0f;
            float endZ = sorted.Count > 0 ? sorted[sorted.Count - 1].Center.z + 1f : start.z;
            // Generous upper bound on time: the whole course at a crawl.
            int maxSteps = Mathf.CeilToInt((endZ - start.z) / 5f / step) + 1;
            for (int i = 0; i < maxSteps && next < sorted.Count; i++)
            {
                Gate gate = sorted[next];
                Vector2 stick = Pilot(state, gate.Center, tuning);
                Vector3 before = state.Position;
                RailFlightModel.Step(ref state, stick, 0f, throttle, tuning, headingLimit, step);
                Vector3 after = state.Position;
                time += step;
                if (Mathf.Abs(after.x) > bounds.x - 1f || after.y < 1f || after.y > bounds.y - 1f)
                    return new Result(passed, sorted.Count, firstMiss < 0 ? next : firstMiss, true, made);
                for (int g = next; g < sorted.Count; g++)
                    if (sorted[g].Timed && triggeredAt[g] < 0f && after.z >= sorted[g].Center.z - sorted[g].TriggerDistance)
                        triggeredAt[g] = time;
                while (next < sorted.Count && after.z >= sorted[next].Center.z && before.z < sorted[next].Center.z)
                {
                    Gate crossed = sorted[next];
                    Vector3 at = Vector3.Lerp(before, after, (crossed.Center.z - before.z) / (after.z - before.z));
                    bool inside = crossed.Contains(new Vector2(at.x - crossed.Center.x, at.y - crossed.Center.y));
                    bool inTime = !crossed.Timed || time - triggeredAt[next] <= crossed.Window;
                    if (inside && inTime) { passed++; made[next] = true; }
                    else if (firstMiss < 0) firstMiss = next;
                    next++;
                }
            }
            return new Result(passed, sorted.Count, firstMiss, false, made);
        }

        /// <summary>Banks toward the gate and pitches toward its height, levelling as it lines up.</summary>
        private static Vector2 Pilot(ArenaFlightState s, Vector3 target, ArenaFlightTuning t)
        {
            Vector3 to = target - s.Position;
            float ahead = Mathf.Max(to.z, 8f);
            float yawError = Mathf.DeltaAngle(s.Yaw, Mathf.Atan2(to.x, ahead) * Mathf.Rad2Deg);
            float pitchError = Mathf.Atan2(to.y, ahead) * Mathf.Rad2Deg - s.Pitch;
            return new Vector2(Mathf.Clamp(yawError / 12f, -1f, 1f), Mathf.Clamp(pitchError / 8f, -1f, 1f));
        }
    }
}

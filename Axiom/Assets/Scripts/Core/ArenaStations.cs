using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>Chooses where an arena lesson arrives so the player keeps flying forward with room to act: authored
    /// stations around the arena, scored by how little the ship must turn and how close they are to a preferred
    /// distance. Angles and distances are measured flat, on the arena floor.</summary>
    public static class ArenaStations
    {
        private const float DegreesPerMetre = 0.1f, BlockedPenalty = 500f, TooClosePenalty = 1000f;

        /// <summary>Degrees between the ship's nose and a station, on the arena floor.</summary>
        public static float AngleOff(Vector3 ship, Vector3 forward, Vector3 station) =>
            Vector2.Angle(new Vector2(forward.x, forward.z), new Vector2(station.x - ship.x, station.z - ship.z));

        /// <param name="nearest">Metres under which a station is used only if every other is closer still.</param>
        /// <param name="blocked">Optional: whether scenery hides a station from the ship. Used only as a last resort.</param>
        /// <param name="avoid">Optional: a station just used, so the lessons move around. Also a last resort.</param>
        /// <returns>The chosen index, or -1 with no stations.</returns>
        public static int Pick(Vector3 ship, Vector3 forward, IReadOnlyList<Vector3> stations, float preferred, float nearest,
            Func<int, bool> blocked = null, int avoid = -1)
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < stations.Count; i++)
            {
                var offset = new Vector2(stations[i].x - ship.x, stations[i].z - ship.z);
                float distance = offset.magnitude;
                float score = AngleOff(ship, forward, stations[i]) + Mathf.Abs(distance - preferred) * DegreesPerMetre;
                if (distance < nearest) score += TooClosePenalty - distance;
                if (i == avoid || blocked != null && blocked(i)) score += BlockedPenalty;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }
    }
}

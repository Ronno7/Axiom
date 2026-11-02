using System;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>The rail's scoring rings: scores each one the ship flies through, from where the flight says it went. A
    /// retry puts the score back to where a checkpoint saved it (<see cref="RingScore"/>).</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ShipFlight))]
    public sealed class RailRings : MonoBehaviour
    {
        [SerializeField] private Transform[] rings = new Transform[0];
        [SerializeField, Min(1f)] private float ringRadius = 12f;

        private readonly RingScore score = new RingScore();
        private ShipFlight flight;

        /// <summary>A ring was flown through and scored (its index).</summary>
        public event Action<int> Scored;
        public int Count => rings.Length;
        public int Hit => score.Hit;
        public Transform Ring(int index) => rings[index];
        /// <summary>Smaller rings (scaled down to fit above or below the flight line) score a smaller circle.</summary>
        public float ScoreRadius(int index) => ringRadius * rings[index].lossyScale.x;
        public RingScore.Snapshot SaveScore() => score.Save();
        public void RestoreScore(RingScore.Snapshot snapshot) => score.Restore(snapshot);

        private void Awake() => flight = GetComponent<ShipFlight>();
        private void OnEnable() => flight.RailFlown += Cross;
        private void OnDisable() => flight.RailFlown -= Cross;

        private void Cross(Vector3 from, Vector3 to)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                Vector3 center = rings[i].position;
                if (from.z >= center.z || to.z < center.z) continue;
                Vector3 crossing = Vector3.Lerp(from, to, (center.z - from.z) / (to.z - from.z));
                if (score.Cross(i, new Vector2(crossing.x - center.x, crossing.y - center.y), ScoreRadius(i)))
                    Scored?.Invoke(i);
            }
        }
    }
}

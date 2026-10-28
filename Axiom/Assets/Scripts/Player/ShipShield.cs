using System;
using GeometricFlight.Combat;
using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>The ship's shield. Enemy shots (IDamageable) and ShipHull collisions call ApplyHit.
    /// Presentation observes Changed and Depleted; this component owns no visuals. LevelFlow clears
    /// TakesHits when the run ends, so a late shot can't play hit feedback over the results (the
    /// protection timer keeps running, so a post-hit blink still ends).</summary>
    [DisallowMultipleComponent]
    public sealed class ShipShield : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int segments = 3;
        [Tooltip("Seconds of protection after a hit, so one crash cannot drain every segment.")]
        [SerializeField, Min(0f)] private float protectionAfterHit = 1.2f;

        /// <summary>Raised after a segment is lost, with the remaining count.</summary>
        public event Action<int> Changed;
        public event Action Depleted;

        public Shield State { get; private set; }
        public bool TakesHits { get; set; } = true;

        private void Awake() => State = new Shield(segments, protectionAfterHit);

        private void Update() => State.Tick(Time.deltaTime);

        /// <summary>Back to full strength (arena restarts after the ship is destroyed).</summary>
        public void Restore()
        {
            State = new Shield(segments, protectionAfterHit);
            Changed?.Invoke(State.Current);
        }

        public bool ApplyHit(int damage = 1)
        {
            if (!TakesHits || !State.TryHit()) return false;
            Changed?.Invoke(State.Current);
            if (State.IsDepleted) Depleted?.Invoke();
            return true;
        }
    }
}

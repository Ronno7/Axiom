using System;
using GeometricFlight.Combat;
using GeometricFlight.Dev;
using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>The ship's shield. Enemy shots (IDamageable) and ShipHull collisions call ApplyHit.
    /// Presentation observes Changed and Depleted; this component owns no visuals. LevelFlow clears
    /// TakesHits when the run ends, so a late shot can't play hit feedback over the results (the
    /// protection timer keeps running, so a post-hit blink still ends). The developer god mode refuses every hit too.</summary>
    [DisallowMultipleComponent]
    public sealed class ShipShield : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int segments = 3;
        [Tooltip("Seconds of protection after a hit, so one crash cannot drain every segment.")]
        [SerializeField, Min(0f)] private float protectionAfterHit = 1.2f;

        /// <summary>The segment count changed, including a restore. For what shows the count.</summary>
        public event Action<int> Changed;
        /// <summary>A segment was lost, with the remaining count. For what reacts to damage.</summary>
        public event Action<int> Hit;
        public event Action Depleted;

        public Shield State { get; private set; }
        public bool TakesHits { get; set; } = true;

        private void Awake() => State = new Shield(segments, protectionAfterHit);

        private void Update() => State.Tick(Time.deltaTime);

        /// <summary>Back to full strength (checkpoints, and control returning after a rebuild, which also starts the
        /// protection window so the first frames of flight are safe).</summary>
        public void Restore(bool protect = false)
        {
            State = new Shield(segments, protectionAfterHit);
            if (protect) State.Protect();
            Changed?.Invoke(State.Current);
        }

        /// <summary>Shows a rebuild's progress: the segments come back one at a time while the ship is down.</summary>
        public void Refill(int count)
        {
            if (State.Current == count) return;
            State.Refill(count);
            Changed?.Invoke(State.Current);
        }

        /// <summary>Destroys the ship regardless of remaining segments or protection.</summary>
        public bool Destroy()
        {
            if (!TakesHits || DevCheats.GodMode || !State.Deplete()) return false;
            RaiseHit();
            Depleted?.Invoke();
            return true;
        }

        public bool ApplyHit(int damage = 1)
        {
            if (!TakesHits || DevCheats.GodMode || !State.TryHit()) return false;
            RaiseHit();
            if (State.IsDepleted) Depleted?.Invoke();
            return true;
        }

        private void RaiseHit()
        {
            Changed?.Invoke(State.Current);
            Hit?.Invoke(State.Current);
        }
    }
}

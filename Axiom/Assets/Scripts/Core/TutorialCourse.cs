using System;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.UI;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// The authored tutorial rail's teaching layer: short captions as the ship reaches each lesson, checkpoints
    /// in the calm stretches between lessons, and a local retry. When the ship is destroyed on the rail it shatters,
    /// the camera holds, and after a moment it is rebuilt at the last checkpoint with a full shield, the score as it
    /// was there, no stray shots and weapons waiting for a fresh press. The run never goes back to the menu entry.
    /// Lessons themselves are ordinary course pieces (hoops, lattice walls, zones, iris, targets) placed in the scene.
    /// </summary>
    public sealed class TutorialCourse : MonoBehaviour
    {
        [Serializable]
        public sealed class Cue
        {
            [Tooltip("World Z where the caption appears.")]
            public float z;
            [Tooltip("{fire}, {arm}, {lower}, {raise} and {boost} name the right control for keyboard or cockpit.")]
            public string caption;
            [Tooltip("Optional: shown instead while weapons are SAFE.")]
            public string whenSafe;
            [Min(0.5f)] public float seconds = 3f;
        }

        [Serializable]
        public sealed class Checkpoint
        {
            [Tooltip("World Z the ship must pass to register it, and where a retry rebuilds the ship.")]
            public float z;
            public string caption = "CHECKPOINT · SHIELD RESTORED";
        }

        [SerializeField] private LevelFlow flow;
        [SerializeField] private ArenaFlight flight;
        [SerializeField] private ShipShield shield;
        [SerializeField] private RailHandlingStudy rail;
        [SerializeField] private SharedEntryStudy entry;
        [SerializeField] private ChaseCamera chase;
        [SerializeField] private PlayerWeapon weapon;
        [Tooltip("Shows the tutorial-only rings readout.")]
        [SerializeField] private LevelUi levelUi;
        [Tooltip("Height of the flight line, where retries rebuild the ship.")]
        [SerializeField] private float flightLineY = 16f;
        [Tooltip("Before the first checkpoint, a retry rebuilds the ship here (the start of the clear lead-in).")]
        [SerializeField] private float startZ;
        [Tooltip("Seconds between the shatter and the rebuild.")]
        [SerializeField, Min(0.5f)] private float rebuildDelay = 2f;
        [SerializeField] private Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
        [SerializeField] private Cue[] cues = Array.Empty<Cue>();

        private MasterArmInput authority;
        private ClosingIris[] irises;
        private int reached = -1, nextCue;
        private string shownRings;
        private float rebuildAt = -1f;
        private OpeningCourseScore.Snapshot saved;

        /// <summary>Z where the next retry rebuilds the ship.</summary>
        public float RetryZ => reached < 0 ? startZ : checkpoints[reached].z;
        public int CheckpointsReached => reached + 1;
        public bool Rebuilding => rebuildAt >= 0f;

        private void OnEnable()
        {
            if (flow == null || flight == null || shield == null || rail == null || entry == null)
            {
                Debug.LogError("Assign the flow, flight, shield, rail and entry to TutorialCourse.", this);
                enabled = false;
                return;
            }
            authority = flight.GetComponent<MasterArmInput>();
            irises = FindObjectsByType<ClosingIris>(FindObjectsSortMode.None);
            Array.Sort(checkpoints, (a, b) => a.z.CompareTo(b.z));
            Array.Sort(cues, (a, b) => a.z.CompareTo(b.z));
            flow.CheckpointRecovery = TakeOverDestroyedShip;
            saved = rail.SaveScore();
        }

        private void OnDisable()
        {
            if (flow != null && flow.CheckpointRecovery == TakeOverDestroyedShip) flow.CheckpointRecovery = null;
        }

        private bool OnRail => !flow.EntryHeld && !rail.Released;

        private bool TakeOverDestroyedShip()
        {
            if (!OnRail) return false;
            flight.InputLocked = true;
            weapon?.ClearProjectiles();
            rebuildAt = Time.time + rebuildDelay;
            entry.Announce("SIGNAL LOST · REBUILDING", rebuildDelay);
            return true;
        }

        private void Update()
        {
            ShowRings();
            if (flow.State != RunState.Playing || flow.EntryHeld) return;
            if (Rebuilding)
            {
                if (Time.time >= rebuildAt) Rebuild();
                return;
            }
            if (!OnRail) return;

            float z = flight.transform.position.z;
            while (reached + 1 < checkpoints.Length && z >= checkpoints[reached + 1].z)
            {
                reached++;
                shield.Restore();
                saved = rail.SaveScore();
                entry.Announce(checkpoints[reached].caption, 2.5f);
            }
            while (nextCue < cues.Length && z >= cues[nextCue].z)
            {
                Cue cue = cues[nextCue++];
                string text = authority != null && !authority.Armed && !string.IsNullOrEmpty(cue.whenSafe)
                    ? cue.whenSafe : cue.caption;
                if (!string.IsNullOrEmpty(text)) entry.Announce(Keys(text), cue.seconds);
            }
        }

        /// <summary>The rings readout belongs to the tutorial: shown on the rail, gone once it is left.</summary>
        private void ShowRings()
        {
            if (levelUi == null) return;
            string text = rail.Released || rail.HoopsTotal == 0 ? null : $"RINGS {rail.HoopsHit}/{rail.HoopsTotal}";
            if (text == shownRings) return;
            shownRings = text;
            levelUi.SetRingsReadout(text);
        }

        private string Keys(string caption)
        {
            bool keyboard = !ControllerService.Instance.State.Connected;
            bool keyThrottle = keyboard || GameSettings.KeyboardThrottleWithController;
            return caption.Replace("{fire}", keyboard ? "SPACE" : "FIRE")
                .Replace("{lower}", keyThrottle ? "Q" : "THROTTLE BACK")
                .Replace("{raise}", keyThrottle ? "E" : "THROTTLE FORWARD")
                .Replace("{boost}", keyThrottle ? "HOLD E" : "TO THE FINAL STOP")
                .Replace("{arm}", authority != null && authority.HasPhysicalSwitch ? "MASTER ARM" : "T");
        }

        private void Rebuild()
        {
            rebuildAt = -1f;
            float z = RetryZ;
            flight.Respawn(new Vector3(0f, flightLineY, z));
            flow.SetShipDestroyed(false);
            shield.Restore();
            rail.RestoreScore(saved);
            // Obstacles ahead of the retry point start fresh.
            foreach (ClosingIris iris in irises)
                if (iris != null && iris.TriggerZ >= z) iris.ResetForStudy();
            weapon?.ClearProjectiles();
            authority?.RequireRelease();
            flight.InputLocked = false;
            chase?.ResetToTarget();
            // Captions after the retry point play again as the ship flies past them.
            nextCue = 0;
            while (nextCue < cues.Length && cues[nextCue].z < z) nextCue++;
            entry.Announce(reached < 0 ? "REBUILT" : "REBUILT AT CHECKPOINT", 2f);
        }
    }
}

using System;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// The authored tutorial rail's teaching layer: one short line as each new idea arrives, flags on what to shoot,
    /// checkpoints in the calm stretches between lessons, and a local retry. The panel stays empty unless there is
    /// something new to do, so a lesson's gentler repeat says nothing. Weapons stretches come from the ship's position and
    /// the systems' state every frame, so a retry finds them by itself. The next ring is always lit ahead of the ship. Missiles and the EMP aren't issued until the arena teaches them, so guns come
    /// first while the instrument stays whole. When the ship is destroyed on the rail it shatters,
    /// the camera holds, and after a moment it is rebuilt at the last checkpoint with a full shield, the score as it
    /// was there, no stray shots and weapons waiting for a fresh press. The run never goes back to the menu entry.
    /// Lessons themselves are ordinary course pieces (hoops, lattice walls, zones, iris, targets) placed in the scene.
    /// </summary>
    public sealed class TutorialCourse : MonoBehaviour
    {
        [Serializable]
        public sealed class Cue
        {
            [Tooltip("World Z where the line or stretch begins.")]
            public float z;
            [Tooltip("World Z where a weapons stretch ends. 0: a one-off line for Seconds.")]
            public float untilZ;
            [Tooltip("Empty says nothing. {fire}, {arm}, {lower}, {raise}, {boost}, {lock} and {emp} name the right control "
                   + "(TutorialCaptions). On a stretch, the line stands until the player has landed a hit.")]
            public string line;
            [Tooltip("Needs weapons: while SAFE the panel asks for MASTER ARM instead, and confirms it when it comes.")]
            public bool weapons;
            [Tooltip("Flags the nearest live target in the stretch with SHOOT.")]
            public bool shoot;
            [Min(0.5f)] public float seconds = 4f;
        }

        [Serializable]
        public sealed class Checkpoint
        {
            [Tooltip("World Z the ship must pass to register it, and where a retry rebuilds the ship.")]
            public float z;
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
        [Tooltip("The arena steps after the rail; arena retries rebuild at their checkpoint.")]
        [SerializeField] private ArenaLessons arena;
        [Tooltip("Panel and flag on the HUD.")]
        [SerializeField] private GuidanceDisplay guidance;
        [Tooltip("Lights the next ring.")]
        [SerializeField] private RingPassFlash rings;
        [Tooltip("Pulses its SAFE state when a lesson calls for MASTER ARM.")]
        [SerializeField] private MissileDisplay weaponsInstrument;
        [Tooltip("Metres ahead the next ring is lit and flagged: over 6 s at cruise, over 4 s at full throttle.")]
        [SerializeField, Min(1f)] private float ringLookahead = 200f;
        [Tooltip("Seconds the panel confirms weapons authority.")]
        [SerializeField, Min(0f)] private float armedConfirmSeconds = 1.5f;
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
        private float rebuildAt = -1f, armedAt = float.NegativeInfinity;
        private bool wasArmed, showingLesson, showingFlag, lessonAsksToArm, weaponsIntroduced;
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

        private void Start()
        {
            // Guns come first: the instrument shows its stores empty until the arena issues them.
            flight.GetComponent<LockOnWeapon>()?.Unload();
            flight.GetComponent<EmpWeapon>()?.TakeOffline();
        }

        private void OnDisable()
        {
            if (flow != null && flow.CheckpointRecovery == TakeOverDestroyedShip) flow.CheckpointRecovery = null;
        }

        private bool OnRail => !flow.EntryHeld && !rail.Released;
        private bool InArenaLessons => !flow.EntryHeld && rail.Released && arena != null && arena.Active;

        private bool TakeOverDestroyedShip()
        {
            if (!OnRail && !InArenaLessons) return false;
            if (!OnRail) arena.ShipDestroyed();
            flight.InputLocked = true;
            weapon?.ClearProjectiles();
            rebuildAt = Time.time + rebuildDelay;
            entry.Announce("SIGNAL LOST", rebuildDelay);
            return true;
        }

        private void Update()
        {
            ShowRings();
            if (flow.State != RunState.Playing) return;
            if (flow.EntryHeld)
            {
                wasArmed = authority != null && authority.Armed;
                return;
            }
            WatchArming();
            if (Rebuilding)
            {
                ClearLesson();
                if (Time.time >= rebuildAt) Rebuild();
                return;
            }
            if (!OnRail)
            {
                ClearLesson();
                return;
            }

            float z = flight.transform.position.z;
            while (reached + 1 < checkpoints.Length && z >= checkpoints[reached + 1].z)
            {
                reached++;
                shield.Restore();
                saved = rail.SaveScore();
                levelUi?.ShowShieldNote("CHECKPOINT");
            }
            while (nextCue < cues.Length && z >= cues[nextCue].z)
            {
                Cue cue = cues[nextCue++];
                if (cue.untilZ <= 0f) entry.Announce(TutorialCaptions.Resolve(cue.line, authority), cue.seconds);
            }
            ShowLesson(z);
        }

        /// <summary>A press while SAFE says why nothing fired, unless the panel is already asking for MASTER ARM.</summary>
        private void WatchArming()
        {
            if (authority == null) return;
            bool armed = authority.Armed;
            if (armed != wasArmed)
            {
                wasArmed = armed;
                armedAt = armed ? Time.time : float.NegativeInfinity;
            }
            if (authority.RefusedThisFrame && OnRail && !lessonAsksToArm)
                entry.Announce(TutorialCaptions.Resolve("WEAPONS SAFE · ARM WITH {arm}", authority), 2.5f);
            if (weaponsInstrument != null) weaponsInstrument.CallForArming = lessonAsksToArm || InArenaLessons;
        }

        private void ShowLesson(float z)
        {
            Cue stretch = null;
            foreach (Cue cue in cues)
                if (cue.untilZ > 0f && z >= cue.z && z < cue.untilZ) stretch = cue;
            bool safe = stretch != null && stretch.weapons && authority != null && !authority.Armed;
            Transform target = stretch != null && stretch.shoot ? NearestTarget(stretch, z) : null;
            // Every target in this stretch is gone: nothing left to teach here, and no success flash.
            if (stretch != null && stretch.shoot && target == null && !safe) stretch = null;
            ShowCard(stretch, safe);

            Transform ring = target == null ? NextRing(z) : null;
            rings?.MarkNext(ring);
            if (guidance == null) return;
            showingFlag = target != null || ring != null;
            if (target != null) guidance.ShowObjective(target, "SHOOT");
            else if (ring != null) guidance.ShowObjective(ring);
            else guidance.ClearObjective();
        }

        /// <summary>Only a weapons stretch stands on the panel: the call for MASTER ARM while SAFE, its confirmation, then
        /// the stretch's own line until the player has landed a hit.</summary>
        private void ShowCard(Cue stretch, bool safe)
        {
            if (guidance == null) return;
            lessonAsksToArm = stretch != null && stretch.weapons;
            string line = null;
            bool confirming = false;
            if (stretch != null)
            {
                // A switch already armed is acknowledged, never made to cycle.
                if (stretch.weapons && !weaponsIntroduced)
                {
                    weaponsIntroduced = true;
                    if (!safe) armedAt = Time.time;
                }
                confirming = stretch.weapons && !safe && Time.time - armedAt < armedConfirmSeconds;
                line = safe ? "ARM WITH {arm} TO FIRE" : confirming ? "WEAPONS ARMED"
                    : weapon != null && weapon.Hits > 0 ? null : stretch.line;
            }
            if (string.IsNullOrEmpty(line))
            {
                if (showingLesson) guidance.ClearCard();
                showingLesson = false;
                return;
            }
            showingLesson = true;
            guidance.SetCard(TutorialCaptions.Resolve(line, authority), confirming);
            guidance.ChipsLit = authority != null && line.Contains("{fire}") && authority.RawInput.FireHeld;
        }

        private void ClearLesson()
        {
            lessonAsksToArm = false;
            if (showingLesson) guidance?.ClearCard();
            if (showingFlag)
            {
                guidance?.ClearObjective();
                rings?.MarkNext(null);
            }
            showingLesson = showingFlag = false;
        }

        /// <summary>The nearest live target ahead within this stretch.</summary>
        private Transform NearestTarget(Cue stretch, float z)
        {
            Transform best = null;
            float bestZ = float.PositiveInfinity;
            foreach (ShootableTarget target in ShootableTarget.Active)
            {
                float at = target.transform.position.z;
                if (at <= z || at >= stretch.untilZ || at >= bestZ) continue;
                best = target.transform;
                bestZ = at;
            }
            return best;
        }

        /// <summary>The next ring ahead, once the ship is close enough to act on it.</summary>
        private Transform NextRing(float z)
        {
            Transform best = null;
            float bestZ = z + ringLookahead;
            for (int i = 0; i < rail.HoopsTotal; i++)
            {
                Transform hoop = rail.Hoop(i);
                float at = hoop.position.z;
                if (at <= z || at >= bestZ) continue;
                best = hoop;
                bestZ = at;
            }
            return best;
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

        private void Rebuild()
        {
            rebuildAt = -1f;
            if (rail.Released)
            {
                RebuildInArena();
                return;
            }
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
            entry.Announce("REBUILT", 1.5f);
        }

        /// <summary>An arena retry: rebuilt at the step's arena checkpoint with a full shield and whatever weapons have been
        /// issued so far restocked (as an arena death always restored), and the current step started again. The rail stays
        /// sealed.</summary>
        private void RebuildInArena()
        {
            flight.Respawn(arena.RetryPosition);
            flow.SetShipDestroyed(false);
            shield.Restore();
            arena.Restock();
            weapon?.ClearProjectiles();
            authority?.RequireRelease();
            flight.InputLocked = false;
            chase?.ResetToTarget();
            arena.RestartStep();
            entry.Announce("REBUILT", 1.5f);
        }
    }
}

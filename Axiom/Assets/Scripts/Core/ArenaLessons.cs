using System.Collections.Generic;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// The tutorial's arena steps after the rail releases the ship (first arena room):
    /// <list type="number">
    /// <item>Free flight: an optional arc of rings around a tower; ends when its last ring is passed or after a while.</item>
    /// <item>LOCK: one far target, then a close pair. A step completes on an actual missile launch at a painted lesson
    /// target, and the volley's result plays out first. A target destroyed before that (guns, a crash) comes back
    /// through its portal.</item>
    /// <item>EMP: the guided encounter opens with a small drone group. The prompt appears only once several drones
    /// are surely inside the pulse; pressing EMP while it is spent restarts the group with a fresh charge.</item>
    /// <item>A breather (shield and missiles restored), then the existing encounter wave.</item>
    /// <item>Its clear cancels the next round, restores the ship and ends the tutorial: "TRAINING COMPLETE", then
    /// the results.</item>
    /// </list>
    /// Every step with targets is an <see cref="ArenaEncounter"/> (Start On Play off) that these steps start and stop,
    /// so targets arrive through portals. A completed step adds its targets to the run's results. Where a step is
    /// guided (the single LOCK target so far), the <see cref="GuidanceDisplay"/> card and flag replace its captions,
    /// and every line comes from the weapon's and lesson's actual state.
    /// <see cref="TutorialCourse"/> owns retry and calls <see cref="ShipDestroyed"/> and <see cref="RestartStep"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaLessons : MonoBehaviour
    {
        public enum Step { Waiting, FreeFlight, LockSingle, LockPair, EmpGroup, Breather, Encounter, Ending, Done }

        [SerializeField] private RailHandlingStudy rail;
        [SerializeField] private LockOnWeapon missiles;
        [SerializeField] private SharedEntryStudy entry;
        [SerializeField] private LevelFlow flow;
        [SerializeField] private ShipShield shield;
        [Tooltip("Where an arena retry rebuilds the ship (just inside the sealed mouth, facing +Z).")]
        [SerializeField] private Transform retryPose;
        [Tooltip("Where a retry rebuilds the ship from the EMP group on (nearer the fight).")]
        [SerializeField] private Transform encounterRetryPose;
        [Tooltip("The optional free-flight arc, in order.")]
        [SerializeField] private RingGate[] arc = new RingGate[0];
        [SerializeField, Min(5f)] private float freeFlightSeconds = 30f;
        [Tooltip("The single far LOCK target (Start On Play off).")]
        [SerializeField] private ArenaEncounter lockSingleWave;
        [Tooltip("The close LOCK pair (Start On Play off).")]
        [SerializeField] private ArenaEncounter lockPairWave;
        [Tooltip("After a valid launch, seconds the volley gets to play out before the next step.")]
        [SerializeField, Min(0.5f)] private float volleySeconds = 4f;
        [Tooltip("The EMP lesson's drone group (Start On Play off).")]
        [SerializeField] private ArenaEncounter empGroup;
        [Tooltip("How many drones must be surely inside the pulse before the EMP prompt appears.")]
        [SerializeField, Min(1)] private int empPromptCount = 3;
        [Tooltip("Metres kept clear of the pulse's edge, for the ship's travel before the press.")]
        [SerializeField, Min(0f)] private float empPromptMargin = 30f;
        [SerializeField, Min(1f)] private float breatherSeconds = 8f;
        [Tooltip("The final wave (Start On Play off).")]
        [SerializeField] private ArenaEncounter encounter;
        [Tooltip("Seconds of \"TRAINING COMPLETE\" in flight before the results.")]
        [SerializeField, Min(0.5f)] private float endingSeconds = 4f;
        [Tooltip("Guidance card and flag on the HUD. Empty: captions only.")]
        [SerializeField] private GuidanceDisplay guidance;

        private readonly HashSet<ShootableTarget> lockedLastFrame = new HashSet<ShootableTarget>();
        private MasterArmInput authority;
        private ShipFlight ship;
        private EmpWeapon emp;
        private float stepStarted, launchedAt = -1f;
        private int firedSeen;
        private bool empPrompted, empOffered, empUsed, empHeldBefore, gunKilled;

        public Step Current { get; private set; } = Step.Waiting;
        public bool Active => Current != Step.Waiting;
        public Vector3 RetryPosition => Current >= Step.EmpGroup && encounterRetryPose != null ? encounterRetryPose.position
            : retryPose != null ? retryPose.position : transform.position;
        /// <summary>The wave the current step runs, if any.</summary>
        public ArenaEncounter CurrentWave => WaveFor(Current);

        private void Awake()
        {
            authority = missiles != null ? missiles.GetComponent<MasterArmInput>() : null;
            ship = missiles != null ? missiles.GetComponent<ShipFlight>() : null;
            emp = missiles != null ? missiles.GetComponent<EmpWeapon>() : null;
        }

        private void Update()
        {
            if (rail == null || missiles == null || entry == null) return;
            if (Current == Step.Waiting)
            {
                if (rail.Released) Begin(Step.FreeFlight);
                return;
            }
            switch (Current)
            {
                case Step.FreeFlight:
                    bool arcDone = arc.Length > 0 && arc[arc.Length - 1] != null && arc[arc.Length - 1].WasPassed;
                    if (arcDone || Time.time - stepStarted >= freeFlightSeconds) Begin(Step.LockSingle);
                    break;
                case Step.LockSingle:
                case Step.LockPair:
                    WatchLock(CurrentWave);
                    if (Current == Step.LockSingle && guidance != null) GuideLock(CurrentWave);
                    break;
                case Step.EmpGroup:
                    WatchEmp();
                    break;
                case Step.Breather:
                    if (Time.time - stepStarted >= breatherSeconds) Begin(Step.Encounter);
                    break;
                case Step.Encounter:
                    if (Cleared(encounter)) Begin(Step.Ending);
                    break;
                case Step.Ending:
                    if (Time.time - stepStarted >= endingSeconds && flow != null && flow.State == RunState.Playing)
                    {
                        Current = Step.Done;
                        flow.Complete();
                    }
                    break;
            }
        }

        /// <summary>The ship was just destroyed: a live wave stands down (its retry starts it again).</summary>
        public void ShipDestroyed() => CurrentWave?.Defeat();

        /// <summary>Starts the current step again from its beginning (a retry after the ship was rebuilt).</summary>
        public void RestartStep()
        {
            if (Current == Step.Waiting || Current == Step.Done) return;
            Begin(Current);
        }

        private ArenaEncounter WaveFor(Step step) => step == Step.LockSingle ? lockSingleWave
            : step == Step.LockPair ? lockPairWave : step == Step.EmpGroup ? empGroup
            : step == Step.Encounter ? encounter : null;

        private void Begin(Step step)
        {
            // A step without its wave is skipped.
            while (step >= Step.LockSingle && step <= Step.Encounter && step != Step.Breather && WaveFor(step) == null)
                step++;
            ArenaEncounter previous = CurrentWave, next = WaveFor(step);
            if (previous != null && previous != next) previous.Stop(); // Leftovers go; no next round is scheduled.
            Current = step;
            stepStarted = Time.time;
            launchedAt = -1f;
            gunKilled = false;
            guidance?.ClearCard();
            guidance?.ClearObjective();
            firedSeen = missiles.MissilesFired;
            lockedLastFrame.Clear();
            next?.Restart();
            switch (step)
            {
                case Step.FreeFlight:
                    foreach (RingGate ring in arc) if (ring != null) ring.ResetPass();
                    Say("FREE FLIGHT", 3f);
                    break;
                case Step.LockSingle:
                    if (guidance == null) Say("MISSILES · HOLD {lock} TO PAINT · RELEASE TO FIRE", 6f);
                    break;
                case Step.LockPair:
                    Say("AGAIN · PAINT BOTH, THEN RELEASE", 4f);
                    break;
                case Step.EmpGroup:
                    empPrompted = empOffered = empUsed = false;
                    Say("DRONES INBOUND", 3f);
                    break;
                case Step.Breather:
                    Say("SHIELD AND MISSILES RESTORED", 3f);
                    break;
                case Step.Encounter:
                    Say("FINAL TEST · USE EVERYTHING", 4f);
                    break;
                case Step.Ending:
                    Say("TRAINING COMPLETE", endingSeconds);
                    break;
            }
        }

        /// <summary>Whether the wave has just been cleared. If so it is stopped (no next round), the ship recovers
        /// and its targets join the results.</summary>
        private bool Cleared(ArenaEncounter wave)
        {
            if (wave.State == null || wave.State.Phase != ArenaWavePhase.Cleared) return false;
            wave.Stop();
            Recover();
            flow?.CountTargets(wave.SpawnCount, wave.SpawnCount);
            return true;
        }

        private void WatchLock(ArenaEncounter wave)
        {
            // A launch counts when missiles leave while a live lesson target was painted on the frame before.
            if (missiles.MissilesFired > firedSeen)
            {
                if (launchedAt < 0f && lockedLastFrame.Count > 0) launchedAt = Time.time;
                firedSeen = missiles.MissilesFired;
            }
            lockedLastFrame.Clear();
            int alive = 0, destroyed = 0;
            foreach (ShootableTarget t in wave.Live)
            {
                if (t == null) continue;
                if (t.IsDestroyed) destroyed++;
                else alive++;
            }
            if (missiles.State != null)
                foreach (int id in missiles.State.Locks)
                {
                    ShootableTarget t = missiles.Target(id);
                    if (t != null && !t.IsDestroyed && InWave(wave, t)) lockedLastFrame.Add(t);
                }

            if (launchedAt >= 0f)
            {
                if (alive == 0 || Time.time - launchedAt >= volleySeconds)
                {
                    flow?.CountTargets(destroyed, wave.SpawnCount);
                    Begin(Current == Step.LockSingle ? Step.LockPair : Step.EmpGroup);
                }
                return;
            }
            // Destroyed without a launch (guns, a collision): the lesson's targets come back through their portals.
            if (destroyed > 0)
            {
                if (Current == Step.LockSingle && guidance != null) gunKilled = true;
                else Say("USE MISSILES · HOLD {lock}", 3f);
                wave.Restart();
            }
        }

        /// <summary>The single LOCK lesson's card and flag (guidance room, build 1). Each line reports what the weapon
        /// and the lesson actually observe; a held key only fills its chip. The flag gives way while the missile
        /// system shows this target (candidate or lock) and comes back if that feedback goes.</summary>
        private void GuideLock(ArenaEncounter wave)
        {
            if (ship == null || !ship.isActiveAndEnabled || ship.InputLocked)
            {
                guidance.ClearCard();
                guidance.ClearObjective();
                return;
            }
            ShootableTarget target = null;
            foreach (ShootableTarget t in wave.Live)
                if (t != null && !t.IsDestroyed)
                {
                    target = t;
                    break;
                }
            if (target != null) gunKilled = false;
            bool held = authority != null ? authority.RawInput.LockHeld : ship.CurrentInput.LockHeld;
            bool flag = false, emphasis = false;
            string text;
            if (launchedAt >= 0f)
            {
                text = "MISSILES AWAY";
                emphasis = true;
            }
            else if (gunKilled) text = "USE MISSILES";
            else if (target == null) text = "TARGET INBOUND";
            else
            {
                int id = target.GetInstanceID();
                bool locked = false;
                foreach (int lockId in missiles.State.Locks) locked |= lockId == id;
                bool acquiring = missiles.State.CandidateId == id;
                float distance = Vector3.Distance(missiles.transform.position, target.transform.position);
                flag = !locked && !acquiring;
                if (authority != null && !authority.Armed) text = "SAFE · {arm} TO ARM";
                else if (locked) text = "PAINTED · RELEASE {lock} TO FIRE";
                else if (missiles.State.Ammo == 0) text = "MISSILES RELOADING";
                else if (!held) text = "HOLD {lock} TO PAINT THE TARGET";
                else if (distance > missiles.MaximumRange) text = "CLOSE TO " + Mathf.RoundToInt(missiles.MaximumRange) + " m";
                else if (distance < missiles.MinimumRange) text = "TOO CLOSE · OPEN THE RANGE";
                else if (acquiring) text = "KEEP THE TARGET IN THE BRACKETS";
                else text = "TURN TOWARD THE MARKED TARGET";
            }
            guidance.SetCard("MISSILES", TutorialCaptions.Resolve(text, authority, chips: true), emphasis);
            guidance.ChipsLit = held;
            if (flag) guidance.ShowObjective(target.transform, "LOCK TARGET");
            else guidance.ClearObjective();
        }

        private static bool InWave(ArenaEncounter wave, ShootableTarget target)
        {
            foreach (ShootableTarget t in wave.Live) if (t == target) return true;
            return false;
        }

        /// <summary>The EMP lesson: prompt once enough drones are surely inside the pulse, offer a retry once the charge
        /// is spent, and move on when the group is cleared by any means.</summary>
        private void WatchEmp()
        {
            if (Cleared(empGroup))
            {
                Begin(Step.Breather);
                return;
            }
            bool held = ship != null && ship.CurrentInput.EmpHeld;
            bool pressed = held && !empHeldBefore;
            empHeldBefore = held;
            if (empGroup.State == null || empGroup.State.Phase != ArenaWavePhase.Combat || emp == null) return;
            if (emp.Pulsing) empUsed = true;

            int remaining = 0, inside = 0;
            foreach (ShootableTarget drone in empGroup.Live)
            {
                if (drone == null || drone.IsDestroyed) continue;
                remaining++;
                if (SurelyInsidePulse(drone)) inside++;
            }
            bool enough = remaining > 0 && inside >= Mathf.Min(empPromptCount, remaining);
            if (emp.State.Ready)
            {
                if (enough && !empPrompted)
                {
                    empPrompted = true;
                    Say("EMP · PRESS {emp}", 4f);
                }
                return;
            }
            if (emp.Pulsing || remaining == 0) return;
            if (!empOffered && (empUsed || enough))
            {
                empOffered = true;
                Say("EMP SPENT · PRESS {emp} TO RETRY", 4f);
            }
            if (empOffered && pressed)
            {
                // An optional retry: the group again, with a fresh charge. The press itself never fires the pulse.
                emp.ResetCharge();
                Begin(Step.EmpGroup);
            }
        }

        /// <summary>Whether a pulse fired from the ship now would reach the drone wherever it is on its patrol.</summary>
        private bool SurelyInsidePulse(ShootableTarget drone)
        {
            var patrol = drone.GetComponent<LissajousPatrol>();
            Vector3 centre = patrol != null ? patrol.Anchor : drone.transform.position;
            float reach = patrol != null ? patrol.Reach : 0f;
            return Vector3.Distance(centre, missiles.transform.position) + reach <= emp.Radius - empPromptMargin;
        }

        private void Recover()
        {
            shield?.Restore();
            missiles.ResetBank();
        }

        private void Say(string caption, float seconds) =>
            entry.Announce(TutorialCaptions.Resolve(caption, authority), seconds);
    }
}

using System.Collections.Generic;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>
    /// The tutorial's arena steps after the rail releases the ship (first arena room):
    /// <list type="number">
    /// <item>Free flight: an optional arc of rings around a tower; ends when its last ring is passed or after a while.</item>
    /// <item>LOCK: one far target, then a pair. A step completes on an actual missile launch at a painted lesson
    /// target, and the volley's result plays out first. A target destroyed before that (guns, a crash) comes back
    /// through its portal.</item>
    /// <item>EMP: the guided encounter opens with a small drone group. The prompt appears only once several drones
    /// are surely inside the pulse; pressing EMP while it is spent restarts the group with a fresh charge.</item>
    /// <item>A breather (shield and missiles restored), then the existing encounter wave.</item>
    /// <item>Its clear cancels the next round, restores the ship and ends the tutorial: "TRAINING COMPLETE", then
    /// the results.</item>
    /// </list>
    /// Every step with targets is an <see cref="ArenaEncounter"/> (Start On Play off) that these steps start and stop,
    /// so targets arrive through portals. The LOCK and EMP steps arrive at the station that suits where the ship is and
    /// where it is heading (<see cref="ArenaStations"/>), so the lessons lap the arena with room to act. A completed step
    /// adds its targets to the run's results, and the HUD's target count starts over for the arena. The
    /// <see cref="GuidanceDisplay"/> panel and flag carry every instruction, each line from the weapons' and the lesson's
    /// actual state. Each step issues the weapon it teaches.
    /// <see cref="TutorialCourse"/> owns retry and calls <see cref="ShipDestroyed"/> and <see cref="RestartStep"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaLessons : MonoBehaviour
    {
        public enum Step { Waiting, FreeFlight, LockSingle, LockPair, EmpGroup, Breather, Encounter, Ending, Done }

        [SerializeField] private LockOnWeapon missiles;
        [SerializeField] private OpeningEntry entry;
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
        [Tooltip("Guidance panel and flag on the HUD.")]
        [SerializeField] private GuidanceDisplay guidance;
        [Tooltip("Lights the next ring of the arc.")]
        [SerializeField] private RingPassFlash rings;
        [Tooltip("Where the LOCK and EMP steps can arrive, spread around the arena clear of its towers.")]
        [SerializeField] private Transform[] stations = new Transform[0];
        [Tooltip("The distance a step prefers to arrive at, in metres.")]
        [SerializeField, Min(1f)] private float stationDistance = 520f;
        [Tooltip("A LOCK step never arrives closer than this while a farther station exists, in metres: painting needs "
               + "time before the minimum range.")]
        [SerializeField, Min(1f)] private float stationNearest = 480f;
        [Tooltip("The same for the EMP step, in metres. It is taught up close, so it can start nearer.")]
        [SerializeField, Min(1f)] private float empStationNearest = 300f;
        [Tooltip("An arrival waits until a station lies within this many degrees of the nose...")]
        [SerializeField, Range(10f, 180f)] private float stationAngle = 110f;
        [Tooltip("...or for this many seconds, then takes the best there is.")]
        [SerializeField, Min(0f)] private float stationWaitSeconds = 4f;
        [Tooltip("Seconds the final wave's opening call holds once it has arrived.")]
        [SerializeField, Min(0f)] private float clearCallSeconds = 3f;

        private readonly HashSet<ShootableTarget> lockedLastFrame = new HashSet<ShootableTarget>();
        private MasterArmInput authority;
        private ShipFlight ship;
        private EmpWeapon emp;
        private LevelUi levelUi;
        private int arenaDestroyed, arenaTotal;
        private float stepStarted, launchedAt = -1f, waveArrivedAt = -1f;
        private int firedSeen;
        private bool empOffered, empUsed, empHeldBefore, gunKilled, awaitingStation;
        private ShootableTarget waveFlag;
        private int lastStation = -1;

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
            levelUi = guidance != null ? guidance.GetComponent<LevelUi>() : null;
            foreach (ArenaEncounter wave in new[] { lockSingleWave, lockPairWave, empGroup, encounter })
                if (wave != null) arenaTotal += wave.SpawnCount;
        }

        private void Update()
        {
            if (ship == null || entry == null) return;
            if (Current == Step.Waiting)
            {
                if (ship.RailReleased) Begin(Step.FreeFlight);
                return;
            }
            if (awaitingStation) TryPlaceWave();
            switch (Current)
            {
                case Step.FreeFlight:
                    bool arcDone = arc.Length > 0 && arc[arc.Length - 1] != null && arc[arc.Length - 1].WasPassed;
                    if (arcDone || Time.time - stepStarted >= freeFlightSeconds) Begin(Step.LockSingle);
                    else GuideArc();
                    break;
                case Step.LockSingle:
                case Step.LockPair:
                    WatchLock(CurrentWave);
                    if (Current == Step.LockSingle || Current == Step.LockPair) GuideLock(CurrentWave);
                    break;
                case Step.EmpGroup:
                    WatchEmp();
                    break;
                case Step.Breather:
                    if (Time.time - stepStarted >= breatherSeconds) Begin(Step.Encounter);
                    break;
                case Step.Encounter:
                    if (Cleared(encounter)) Begin(Step.Ending);
                    else GuideWave();
                    break;
                case Step.Ending:
                    if (Time.time - stepStarted >= endingSeconds && flow != null && flow.State == RunState.Playing)
                    {
                        Current = Step.Done;
                        flow.Complete();
                    }
                    break;
            }
            ShowTargets();
        }

        /// <summary>The count starts over for the arena: every target its lessons hold, finished ones plus this wave's.</summary>
        private void ShowTargets()
        {
            if (levelUi == null) return;
            ArenaWaveState state = !awaitingStation && CurrentWave != null ? CurrentWave.State : null;
            levelUi.SetTargetCount(arenaDestroyed + (state != null ? state.Total - state.Remaining : 0), arenaTotal);
        }

        /// <summary>Restocks whatever has been issued so far (a retry after the ship was rebuilt).</summary>
        public void Restock()
        {
            if (!missiles.State.Unloaded) missiles.ResetBank();
            if (emp != null && !emp.State.Offline) emp.ResetCharge();
        }

        /// <summary>The ship was just destroyed: a live wave stands down (its retry starts it again).</summary>
        public void ShipDestroyed()
        {
            CurrentWave?.Defeat();
        }

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
            launchedAt = waveArrivedAt = -1f;
            gunKilled = false;
            waveFlag = null;
            ClearGuidance();
            firedSeen = missiles.MissilesFired;
            lockedLastFrame.Clear();
            IssueWeapons(step);
            awaitingStation = step == Step.LockSingle || step == Step.LockPair || step == Step.EmpGroup;
            if (awaitingStation)
            {
                next?.Stop(); // A retry of this step: nothing stays while its new arrival is chosen.
                TryPlaceWave();
            }
            else next?.Restart();
            switch (step)
            {
                case Step.FreeFlight:
                    foreach (RingGate ring in arc) if (ring != null) ring.ResetPass();
                    Say("WELCOME TO THE ARENA", 4f);
                    break;
                case Step.EmpGroup:
                    empOffered = empUsed = false;
                    break;
                case Step.Ending:
                    Say("TRAINING COMPLETE", endingSeconds);
                    break;
            }
        }

        /// <summary>Each step issues the weapon it teaches and says so; the instrument's empty stores load as it does.</summary>
        private void IssueWeapons(Step step)
        {
            if (step >= Step.LockSingle && missiles.State.Unloaded)
            {
                missiles.ResetBank();
                Say("MISSILES ONLINE", 2.5f);
            }
            if (step >= Step.EmpGroup && emp != null && emp.State.Offline)
            {
                emp.ResetCharge();
                Say("EMP ONLINE", 2.5f);
            }
        }

        /// <summary>Brings the step's wave in at the station that suits the ship, keeping the wave's shape. If every
        /// station is behind the ship it waits a moment for the turn. Once the portals show, the wave is committed.</summary>
        private void TryPlaceWave()
        {
            ArenaEncounter wave = CurrentWave;
            if (wave != null && stations.Length > 0)
            {
                var points = new Vector3[stations.Length];
                for (int i = 0; i < points.Length; i++) points[i] = stations[i].position;
                Vector3 from = missiles.transform.position, nose = missiles.transform.forward;
                LayerMask scenery = missiles.GetComponent<PlayerWeapon>().HitLayers;
                int pick = ArenaStations.Pick(from, nose, points, stationDistance,
                    Current == Step.EmpGroup ? empStationNearest : stationNearest,
                    i => Physics.Linecast(from, points[i], scenery, QueryTriggerInteraction.Ignore), lastStation);
                if (ArenaStations.AngleOff(from, nose, points[pick]) > stationAngle
                    && Time.time - stepStarted < stationWaitSeconds) return;
                wave.PlaceAt(points[pick]);
                lastStation = pick;
            }
            awaitingStation = false;
            wave?.Restart();
        }

        /// <summary>Whether the wave has just been cleared. If so it is stopped (no next round), the ship recovers
        /// and its targets join the results.</summary>
        private bool Cleared(ArenaEncounter wave)
        {
            if (wave.State == null || wave.State.Phase != ArenaWavePhase.Cleared) return false;
            wave.Stop();
            Recover();
            flow?.CountTargets(wave.SpawnCount, wave.SpawnCount);
            arenaDestroyed += wave.SpawnCount;
            Say("SECTOR CLEAR", 2.5f);
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
                    arenaDestroyed += destroyed;
                    Begin(Current == Step.LockSingle ? Step.LockPair : Step.EmpGroup);
                }
                return;
            }
            // Destroyed without a launch (guns, a collision): the lesson's targets come back through their portals.
            if (destroyed > 0)
            {
                gunKilled = true;
                wave.Restart();
            }
        }

        private bool Guiding => guidance != null && ship != null && ship.isActiveAndEnabled && !ship.InputLocked;
        private bool Safe => authority != null && !authority.Armed;

        private void ClearGuidance()
        {
            guidance?.ClearCard();
            guidance?.ClearObjective();
            rings?.MarkNext(null);
        }

        /// <summary>The panel's standing line; null leaves it empty.</summary>
        private void Card(string line, bool held = false, bool emphasis = false, bool arrival = false)
        {
            if (line == null)
            {
                guidance.ClearCard();
                return;
            }
            guidance.SetCard(TutorialCaptions.Resolve(line), emphasis, arrival);
            guidance.ChipsLit = held;
        }

        private const string ArmLine = "ARM WITH {arm} TO FIRE";

        /// <summary>Free flight asks for nothing but armed weapons: the arena's welcome has been said, and the arc's next
        /// ring is lit for anyone who wants it.</summary>
        private void GuideArc()
        {
            if (!Guiding)
            {
                ClearGuidance();
                return;
            }
            Card(Safe ? ArmLine : null);
            RingGate next = null;
            for (int i = arc.Length - 1; i >= 0; i--)
            {
                if (arc[i] == null) continue;
                if (arc[i].WasPassed) break;
                next = arc[i];
            }
            rings?.MarkNext(next != null ? next.transform : null);
            if (next != null) guidance.ShowObjective(next.transform);
            else guidance.ClearObjective();
        }

        /// <summary>The LOCK lessons' line and flag (guidance room): hold, then release. Brackets show the painting
        /// itself, so the line changes only when the action does. A held key only fills its chip. The flag gives way while
        /// the missile system shows that target (candidate or lock) and comes back if that feedback goes.</summary>
        private void GuideLock(ArenaEncounter wave)
        {
            if (!Guiding)
            {
                ClearGuidance();
                return;
            }
            ShootableTarget target = null;
            int alive = 0, painted = 0;
            foreach (ShootableTarget t in wave.Live)
            {
                if (t == null || t.IsDestroyed) continue;
                alive++;
                if (IsLocked(t)) painted++;
                else if (target == null || missiles.State.CandidateId == t.GetInstanceID()) target = t;
            }
            if (alive > 0) gunKilled = false;
            bool held = authority != null ? authority.RawInput.LockHeld : ship.CurrentInput.LockHeld;
            bool acquiring = target != null && missiles.State.CandidateId == target.GetInstanceID();
            string line = null;
            if (launchedAt >= 0f) line = null;
            else if (gunKilled) line = "USE MISSILES";
            else if (Safe) line = ArmLine;
            else if (alive == 0) line = null;
            else if (painted == alive) line = "RELEASE {lock} TO FIRE";
            else if (missiles.State.Ammo == 0) line = "MISSILES RELOADING";
            else
            {
                float distance = Vector3.Distance(missiles.transform.position, target.transform.position);
                if (held && distance > missiles.MaximumRange) line = "CLOSE TO " + Mathf.RoundToInt(missiles.MaximumRange) + " m";
                else if (held && distance < missiles.MinimumRange) line = "TOO CLOSE · OPEN THE RANGE";
                else line = alive > 1 ? "PAINT BOTH · HOLD {lock}" : "HOLD {lock} TO PAINT";
            }
            Card(line, held);
            if (launchedAt >= 0f) guidance.ClearObjective();
            else if (alive == 0) FlagArrival(wave, "LOCK");
            else if (target != null && !acquiring) guidance.ShowObjective(target.transform, "LOCK");
            else guidance.ClearObjective();
        }

        private bool IsLocked(ShootableTarget target)
        {
            int id = target.GetInstanceID();
            foreach (int lockId in missiles.State.Locks) if (lockId == id) return true;
            return false;
        }

        /// <summary>The EMP lesson's line: the approach first, then the press once it would surely reach them.</summary>
        private void GuideEmp(int remaining, bool enough)
        {
            if (!Guiding)
            {
                ClearGuidance();
                return;
            }
            bool arriving = empGroup.State == null || empGroup.State.Phase != ArenaWavePhase.Combat;
            bool held = authority != null ? authority.RawInput.EmpHeld : ship.CurrentInput.EmpHeld;
            string line = arriving ? null
                : Safe ? ArmLine
                : emp.Pulsing ? null
                : !emp.State.Ready ? remaining > 0 && empOffered ? "PRESS {emp} TO RETRY" : null
                : enough ? "PRESS {emp}"
                : "CLOSE ON THE DRONES";
            Card(line, held, emphasis: enough);
            if (arriving) FlagArrival(empGroup, "INBOUND");
            else if (emp.Pulsing || enough && emp.State.Ready) guidance.ClearObjective();
            else
            {
                ShootableTarget drone = NearestLive(empGroup);
                if (drone != null) guidance.ShowObjective(drone.transform, "CLOSE IN");
                else guidance.ClearObjective();
            }
        }

        /// <summary>The final wave's arrival and opening call, then nothing but the flag: it shows where the next fight
        /// is, and stays on one enemy until it is destroyed so the pointer never jumps between directions.</summary>
        private void GuideWave()
        {
            if (!Guiding)
            {
                ClearGuidance();
                return;
            }
            ArenaWaveState wave = encounter.State;
            bool arriving = wave == null || wave.Phase != ArenaWavePhase.Combat;
            if (!arriving && waveArrivedAt < 0f) waveArrivedAt = Time.time;
            if (arriving) Card("PROGRAMS INCOMING", arrival: true);
            else if (Safe) Card(ArmLine);
            else Card(Time.time - waveArrivedAt < clearCallSeconds ? "CLEAR THE SECTOR" : null);
            if (arriving)
            {
                FlagArrival(encounter, "INBOUND");
                return;
            }
            if (waveFlag == null || waveFlag.IsDestroyed) waveFlag = NearestLive(encounter);
            if (waveFlag != null) guidance.ShowObjective(waveFlag.transform, "SHOOT");
            else guidance.ClearObjective();
        }

        /// <summary>Flags the wave's nearest portal, once it has one.</summary>
        private void FlagArrival(ArenaEncounter wave, string action)
        {
            if (awaitingStation)
            {
                guidance.ClearObjective();
                return;
            }
            Vector3 from = missiles.transform.position;
            Transform best = wave.SpawnPoint(0);
            for (int i = 1; i < wave.SpawnCount; i++)
                if ((wave.SpawnPosition(i) - from).sqrMagnitude < (best.position - from).sqrMagnitude) best = wave.SpawnPoint(i);
            guidance.ShowObjective(best, action);
        }

        private ShootableTarget NearestLive(ArenaEncounter wave)
        {
            Vector3 from = missiles.transform.position;
            ShootableTarget best = null;
            foreach (ShootableTarget t in wave.Live)
            {
                if (t == null || t.IsDestroyed) continue;
                if (best == null || (t.transform.position - from).sqrMagnitude < (best.transform.position - from).sqrMagnitude) best = t;
            }
            return best;
        }

        private static bool InWave(ArenaEncounter wave, ShootableTarget target)
        {
            foreach (ShootableTarget t in wave.Live) if (t == target) return true;
            return false;
        }

        /// <summary>The EMP lesson: the press is asked for once enough drones are surely inside the pulse, a retry is
        /// offered once the charge is spent, and the step ends when the group is cleared by any means.</summary>
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
            if (emp == null) return;
            int remaining = 0, inside = 0;
            foreach (ShootableTarget drone in empGroup.Live)
            {
                if (drone == null || drone.IsDestroyed) continue;
                remaining++;
                if (SurelyInsidePulse(drone)) inside++;
            }
            bool enough = remaining > 0 && inside >= Mathf.Min(empPromptCount, remaining);
            if (empGroup.State != null && empGroup.State.Phase == ArenaWavePhase.Combat)
            {
                if (emp.Pulsing) empUsed = true;
                if (!emp.State.Ready && !emp.Pulsing && remaining > 0 && (empUsed || enough)) empOffered = true;
                if (empOffered && pressed && !emp.State.Ready && !emp.Pulsing)
                {
                    // An optional retry: the group again, with a fresh charge. The press itself never fires the pulse.
                    emp.ResetCharge();
                    Begin(Step.EmpGroup);
                    return;
                }
            }
            GuideEmp(remaining, enough);
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

        private void Say(string line, float seconds) =>
            entry.Announce(TutorialCaptions.Resolve(line), seconds);
    }
}

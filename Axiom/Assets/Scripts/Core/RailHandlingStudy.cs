using System;
using System.Collections.Generic;
using System.IO;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Core
{
    /// <summary>Opt-in straight-rail comparison, not the production journey lifecycle. One flight actor,
    /// camera and weapon set survive the release. LevelFlow still owns pause, damage, results and retry.</summary>
    [DefaultExecutionOrder(30), DisallowMultipleComponent, RequireComponent(typeof(ArenaFlight))]
    public sealed class RailHandlingStudy : MonoBehaviour
    {
        [SerializeField] private RailTravelMode candidate = RailTravelMode.NoseLed;
        [Tooltip("Study range. Wider cones need a separate time-integration study before use.")]
        [SerializeField, Range(40f, 55f)] private float headingLimit = 45f;
        [SerializeField] private float releaseZ = 900f;
        [SerializeField] private Vector2 envelopeX = new Vector2(-110f, 110f);
        [SerializeField] private Vector2 envelopeY = new Vector2(10f, 180f);
        [SerializeField] private LevelFlow flow;
        [SerializeField] private Transform[] hoops;
        [SerializeField, Min(1f)] private float hoopRadius = 12f;
        [SerializeField] private ClosingIris iris;
        [Tooltip("The comparison's restart and report keys (F1, F2, M, Backspace, F8). Off in authored runs.")]
        [SerializeField] private bool testKeys = true;
        private static bool hasSelection, selectedFullRotation = true;
        private static RailTravelMode selectedCandidate;
        private ArenaFlight flight;
        private MasterArmInput authority;
        private ShipShield shield;
        private ShipHull hull;
        private LockOnWeapon missiles;
        private EmpWeapon emp;
        private int lastShield;
        private float nextSample;
        private Vector2 lastStick;
        private readonly OpeningCourseScore score = new OpeningCourseScore();
        private readonly List<Sample> samples = new List<Sample>();
        private static readonly ArenaLimits DistantLimits = new ArenaLimits(Vector3.zero, 1e9f, -1e9f, 1e9f);

        public RailTravelMode Candidate => candidate;
        public bool Released { get; private set; }
        public float HeadingLimit => headingLimit;
        public LevelFlow Flow => flow;
        public int Score => score.Score;
        public int HoopsHit => score.GatesHit;
        public float ReleaseZ => releaseZ;
        /// <summary>A ring was flown through and scored (its index in the hoop list).</summary>
        public event System.Action<int> HoopScored;
        public Transform Hoop(int index) => hoops[index];
        /// <summary>Smaller rings (scaled down to fit above or below the flight line) score a smaller circle.</summary>
        public float HoopScoreRadius(int index) => hoopRadius * hoops[index].lossyScale.x;
        public OpeningCourseScore.Snapshot SaveScore() => score.Save();
        public void RestoreScore(OpeningCourseScore.Snapshot snapshot) => score.Restore(snapshot);
        public int HoopsTotal => hoops == null ? 0 : hoops.Length;
        public float HeadingLimitSeconds { get; private set; }
        public float BankWithoutTurnSeconds { get; private set; }
        public float OutsideEnvelopeSeconds { get; private set; }
        public float ContactSeconds { get; private set; }
        public int ShieldLost { get; private set; }
        public int StickReversals { get; private set; }
        public SeamRecord Seam { get; private set; }
        /// <summary>An exit outside the marked passage, or one corrected by existing arena bounds,
        /// cannot be used as evidence of an unassisted movement-law comparison.</summary>
        public bool ReleaseComparisonInvalid => Seam != null && (Seam.outsideEnvelope || Seam.arenaBoundaryCorrected);
        public string LastReportPath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection()
        {
            hasSelection = false;
            selectedFullRotation = true;
            selectedCandidate = RailTravelMode.NoseLed;
        }

        private void Awake()
        {
            flight = GetComponent<ArenaFlight>();
            authority = GetComponent<MasterArmInput>();
            shield = GetComponent<ShipShield>();
            hull = GetComponent<ShipHull>();
            missiles = GetComponent<LockOnWeapon>();
            emp = GetComponent<EmpWeapon>();
            if (hasSelection) candidate = selectedCandidate;
        }

        private void Start()
        {
            if (hasSelection) flight.SetFullRotation(selectedFullRotation);
            lastShield = shield.State.Current;
            // A study starts commissioned. This is never called at the release seam.
            authority.PrepareRun(false);
            authority.WeaponsAvailable = true;
        }

        /// <summary>Integrates to the actual release crossing, then continues the same state in arena
        /// flight. Candidate B's changed velocity law is recorded, never concealed by a blend.</summary>
        public Vector3 Step(ref ArenaFlightState state, Vector2 stick, float twist, float throttle,
            ArenaFlightTuning tuning, ArenaLimits limits, FlightInput raw, float dt)
        {
            if (dt <= 0f) return Vector3.zero;
            Vector3 start = state.Position;
            if (Released)
            {
                StepArenaAndRecordBoundary(ref state, stick, twist, throttle, tuning, limits, dt);
                return (state.Position - start) / dt;
            }

            ArenaFlightState before = state;
            ArenaFlightState predicted = state;
            RailFlightModel.Step(ref predicted, stick, twist, throttle, tuning, candidate, headingLimit, dt);
            float railTime = dt;
            if (before.Position.z >= releaseZ) railTime = 0f;
            else if (predicted.Position.z >= releaseZ)
            {
                // Monotonic forward travel makes a bounded bisection valid for both candidates.
                float low = 0f, high = dt;
                for (int i = 0; i < 16; i++)
                {
                    float mid = (low + high) * 0.5f;
                    ArenaFlightState trial = before;
                    RailFlightModel.Step(ref trial, stick, twist, throttle, tuning, candidate, headingLimit, mid);
                    if (trial.Position.z < releaseZ) low = mid; else high = mid;
                }
                railTime = high;
            }
            Vector3 railSegmentVelocity = RailFlightModel.Step(ref state, stick, twist, throttle, tuning, candidate, headingLimit, railTime);
            if (railTime > 0f) TrackRail(before, state, raw, railTime);
            CrossHoops(start, state.Position);
            if (state.Position.z >= releaseZ)
            {
                Vector3 railVelocity = InstantRailVelocity(state, candidate);
                Seam = new SeamRecord
                {
                    position = state.Position, attitude = state.Rotation, speed = state.Speed,
                    yaw = state.Yaw, pitch = state.Pitch, bank = state.Bank,
                    rawThrottle = raw.Throttle, throttle = throttle, steering = raw.Steering, twist = raw.Twist,
                    fire = raw.FireHeld, lockHeld = raw.LockHeld, empHeld = raw.EmpHeld,
                    physicalFire = authority.RawInput.FireHeld, physicalLock = authority.RawInput.LockHeld,
                    physicalEmp = authority.RawInput.EmpHeld,
                    shield = shield.State.Current, missiles = missiles.State.Ammo,
                    empReady = emp.State.Ready, empPulsing = emp.Pulsing, armed = authority.Armed,
                    railVelocity = railVelocity, arenaVelocity = state.Forward * state.Speed,
                    velocityChange = (state.Forward * state.Speed - railVelocity).magnitude,
                    railSegmentVelocity = railSegmentVelocity, railSegmentSeconds = railTime,
                    outsideEnvelope = OutsideEnvelope(state.Position),
                    nextPosition = state.Position, nextAttitude = state.Rotation,
                    arenaUnboundedPosition = state.Position, arenaUnboundedAttitude = state.Rotation,
                    time = flow.ElapsedTime + railTime
                };
                Released = true; // The only state change at the seam; no input or weapon reset.
                StepArenaAndRecordBoundary(ref state, stick, twist, throttle, tuning, limits, dt - railTime);
                // No file IO on the judged transition frame. F8 or an explicit retry exports evidence.
            }
            return (state.Position - start) / dt;
        }

        public static Vector3 InstantRailVelocity(ArenaFlightState state, RailTravelMode mode) =>
            (mode == RailTravelMode.NoseLed ? state.Forward
                : Vector3.forward + Vector3.ProjectOnPlane(state.Forward, Vector3.forward)) * state.Speed;

        private static void StepArena(ref ArenaFlightState state, Vector2 stick, float twist, float throttle,
            ArenaFlightTuning tuning, ArenaLimits limits, float dt)
        {
            if (state.FullRotation) ArenaFlightModel.StepFull(ref state, stick, twist, throttle, tuning, limits, dt);
            else ArenaFlightModel.Step(ref state, stick, twist, throttle, tuning, limits, dt);
        }

        private void StepArenaAndRecordBoundary(ref ArenaFlightState state, Vector2 stick, float twist,
            float throttle, ArenaFlightTuning tuning, ArenaLimits limits, float dt)
        {
            if (dt <= 0f) return;
            if (Seam == null || Seam.arenaBoundarySampled)
            {
                StepArena(ref state, stick, twist, throttle, tuning, limits, dt);
                return;
            }

            // Compare the first positive arena step against the same controls without arena bounds.
            // This is evidence only: the real state still receives exactly the existing arena step.
            // A crossing at the end of a frame defers this sample, including across a pause.
            Vector3 start = state.Position;
            ArenaFlightState unbounded = state;
            StepArena(ref unbounded, stick, twist, throttle, tuning, DistantLimits, dt);
            StepArena(ref state, stick, twist, throttle, tuning, limits, dt);
            Seam.arenaBoundarySampled = true;
            Seam.arenaSegmentSeconds = dt;
            Seam.arenaSegmentVelocity = (state.Position - start) / dt;
            Seam.nextPosition = state.Position;
            Seam.nextAttitude = state.Rotation;
            Seam.arenaUnboundedPosition = unbounded.Position;
            Seam.arenaUnboundedAttitude = unbounded.Rotation;
            Seam.arenaBoundaryPositionCorrection = state.Position - unbounded.Position;
            Seam.arenaBoundaryAngleDegrees = Quaternion.Angle(unbounded.Rotation, state.Rotation);
            Seam.arenaBoundaryCorrected = Seam.arenaBoundaryPositionCorrection.sqrMagnitude > 1e-8f
                || Seam.arenaBoundaryAngleDegrees > 0.01f;
        }

        private bool OutsideEnvelope(Vector3 position) => position.x < envelopeX.x || position.x > envelopeX.y
            || position.y < envelopeY.x || position.y > envelopeY.y;

        private void TrackRail(ArenaFlightState before, ArenaFlightState after, FlightInput raw, float dt)
        {
            if (candidate == RailTravelMode.NoseLed && Vector3.Angle(after.Forward, Vector3.forward) >= headingLimit - 0.1f)
                HeadingLimitSeconds += dt;
            if (Mathf.Abs(after.Bank) > 15f && Vector3.Angle(before.Forward, after.Forward) / dt < 1f)
                BankWithoutTurnSeconds += dt;
            if (OutsideEnvelope(after.Position)) OutsideEnvelopeSeconds += dt;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(raw.Steering[axis]) < 0.2f) continue;
                if (Mathf.Abs(lastStick[axis]) >= 0.2f && Mathf.Sign(raw.Steering[axis]) != Mathf.Sign(lastStick[axis]))
                    StickReversals++;
                lastStick[axis] = raw.Steering[axis];
            }
        }

        private void CrossHoops(Vector3 from, Vector3 to)
        {
            if (hoops == null) return;
            for (int i = 0; i < hoops.Length; i++)
            {
                Vector3 center = hoops[i].position;
                if (from.z >= center.z || to.z < center.z) continue;
                Vector3 crossing = Vector3.Lerp(from, to, (center.z - from.z) / (to.z - from.z));
                if (score.CrossGate(i, new Vector2(crossing.x - center.x, crossing.y - center.y), HoopScoreRadius(i)))
                    HoopScored?.Invoke(i);
            }
        }

        private void Update()
        {
            if (flow.EntryHeld)
            {
                lastShield = shield.State.Current;
                return;
            }
            Keyboard keys = testKeys ? Keyboard.current : null;
            if (keys != null)
            {
                if (keys.f1Key.wasPressedThisFrame) { Restart(RailTravelMode.NoseLed, flight.FullRotation); return; }
                if (keys.f2Key.wasPressedThisFrame) { Restart(RailTravelMode.CourseDriven, flight.FullRotation); return; }
                if (keys.mKey.wasPressedThisFrame) { Restart(candidate, !flight.FullRotation); return; }
                if (keys.backspaceKey.wasPressedThisFrame) { Restart(candidate, flight.FullRotation); return; }
                if (keys.f8Key.wasPressedThisFrame) ExportReport();
            }
            ShieldLost += Mathf.Max(0, lastShield - shield.State.Current);
            lastShield = shield.State.Current;
            if (flow.State != RunState.Playing || Time.deltaTime <= 0f) return;
            if (!Released && hull.Overlaps(transform.position, true)) ContactSeconds += Time.deltaTime;
            if (flow.ElapsedTime >= nextSample && samples.Count < 6000)
            {
                nextSample = flow.ElapsedTime + 0.1f;
                samples.Add(new Sample { time = flow.ElapsedTime, position = transform.position,
                    attitude = transform.rotation, velocity = flight.Velocity, throttle = flight.CurrentInput.Throttle,
                    steering = flight.CurrentInput.Steering, twist = flight.CurrentInput.Twist, released = Released,
                    heading = Vector3.Angle(transform.forward, Vector3.forward), shield = shield.State.Current });
            }
        }

        public void Restart(RailTravelMode mode, bool fullRotation)
        {
            ExportReport();
            selectedCandidate = mode; selectedFullRotation = fullRotation; hasSelection = true;
            flow.Retry();
        }

        public void ExportReport()
        {
            // Study data stays in the Editor; a player's disk is left alone.
            if (!Application.isEditor || flight == null || flow == null) return;
            var report = new Report { candidate = candidate.ToString(), handling = flight.FullRotation ? "Roll & Pitch" : "Bank & Turn",
                elapsed = flow.ElapsedTime, score = Score, hoopsHit = HoopsHit, hoopsTotal = HoopsTotal,
                headingLimitSeconds = HeadingLimitSeconds, bankWithoutTurnSeconds = BankWithoutTurnSeconds,
                outsideEnvelopeSeconds = OutsideEnvelopeSeconds, contactSeconds = ContactSeconds,
                shieldLost = ShieldLost, stickReversals = StickReversals, irisCrossed = iris != null && iris.Crossed,
                irisPassed = iris != null && iris.Passed, seam = Seam, samples = samples.ToArray() };
            string directory = Path.Combine(Application.dataPath, "..", "Logs", "RailStudy");
            Directory.CreateDirectory(directory);
            LastReportPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true));
        }

        [Serializable] public sealed class SeamRecord
        {
            // railVelocity/arenaVelocity compare the instantaneous laws at the identical seam pose.
            // Segment velocities below are measured displacement/time; zero duration means no sample.
            // Arena data describes the first positive arena substep, which may be on the next frame.
            public Vector3 position, nextPosition, railVelocity, arenaVelocity, railSegmentVelocity, arenaSegmentVelocity,
                arenaUnboundedPosition, arenaBoundaryPositionCorrection;
            public Quaternion attitude, nextAttitude, arenaUnboundedAttitude;
            public Vector2 steering;
            public float time, speed, yaw, pitch, bank, rawThrottle, throttle, twist, velocityChange,
                railSegmentSeconds, arenaSegmentSeconds, arenaBoundaryAngleDegrees;
            public int shield, missiles;
            public bool fire, lockHeld, empHeld, physicalFire, physicalLock, physicalEmp, empReady, empPulsing, armed;
            public bool outsideEnvelope, arenaBoundarySampled, arenaBoundaryCorrected;
        }
        [Serializable] public sealed class Sample
        {
            public float time, throttle, twist, heading;
            public Vector3 position, velocity;
            public Quaternion attitude;
            public Vector2 steering;
            public int shield;
            public bool released;
        }
        [Serializable] private sealed class Report
        {
            public string candidate, handling;
            public float elapsed, headingLimitSeconds, bankWithoutTurnSeconds, outsideEnvelopeSeconds, contactSeconds;
            public int score, hoopsHit, hoopsTotal, shieldLost, stickReversals;
            public bool irisCrossed, irisPassed;
            public SeamRecord seam;
            public Sample[] samples;
        }
    }
}

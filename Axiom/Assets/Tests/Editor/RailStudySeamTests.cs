using System;
using System.Collections.Generic;
using System.Reflection;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>Exercises the real study component's boundary decision with initialized state holders.
    /// Objects stay inactive: no input devices, scene lifecycle, renderer, pools or file export are used.</summary>
    public sealed class RailStudySeamTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly ArenaLimits Arena = new ArenaLimits(Vector3.zero, 10000f, -10000f, 10000f);
        private GameObject actor;
        private ArenaFlight flight;
        private RailHandlingStudy study;
        private MasterArmInput authority;
        private WeaponAuthority weaponAuthority;
        private ShipShield shield;
        private LockOnWeapon missiles;
        private EmpWeapon emp;
        private LevelFlow flow;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Rail seam test (inactive)");
            actor.SetActive(false);
            flight = actor.AddComponent<ArenaFlight>();
            study = actor.AddComponent<RailHandlingStudy>();
            var source = actor.AddComponent<CombinedFlightInput>();
            source.KeyboardOnly = true; // Reading Armed must never create the real serial service.
            authority = actor.AddComponent<MasterArmInput>();
            Set(authority, "source", source);
            Set(authority, "keyboardArmed", true);
            authority.WeaponsAvailable = true;
            weaponAuthority = (WeaponAuthority)Get(authority, "authority");
            weaponAuthority.Filter(default, true, true);

            shield = actor.AddComponent<ShipShield>();
            var shieldState = new Shield(3, 10f);
            shieldState.TryHit();
            SetAuto(shield, "State", shieldState);

            missiles = actor.AddComponent<LockOnWeapon>();
            var missileState = new MissileLockState(4, 0.1f, 0.1f, 2f, 5f);
            var targets = new HashSet<int> { 1, 2 };
            missileState.Step(0.2f, true, true, 1, targets, targets);
            missileState.Step(0.01f, false, true, 0, targets, targets); // Spend one round.
            missileState.Step(0.2f, true, true, 2, targets, targets); // Carry a painted target across.
            SetAuto(missiles, "State", missileState);

            emp = actor.AddComponent<EmpWeapon>();
            emp.State.Step(false, true);
            emp.State.Step(true, true);
            SetAuto(emp, "Pulsing", true);
            SetAuto(emp, "Origin", new Vector3(1f, 70f, 2f));
            SetAuto(emp, "Orientation", Quaternion.Euler(10f, 20f, 30f));
            Set(emp, "age", 0.8f);

            flow = actor.AddComponent<LevelFlow>();
            Set(flow, "targets", Array.Empty<ShootableTarget>());
            Set(flow, "shield", shield);
            Set(study, "authority", authority);
            Set(study, "shield", shield);
            Set(study, "missiles", missiles);
            Set(study, "emp", emp);
            Set(study, "flow", flow);
            Set(study, "releaseZ", 10f);
            // Leave study.flight null: Step does not need it; ExportReport then exits without writing.
        }

        [TearDown]
        public void TearDown()
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
        }

        private static ArenaFlightState State(Quaternion attitude, float z = 9f) => new ArenaFlightState
        {
            Position = new Vector3(13f, 70f, z), Speed = 30f,
            FullRotation = true, Attitude = attitude,
        };

        [Test]
        public void NoseLedReleaseKeepsDisplacedInvertedPoseAndHeldRollWithoutVelocityImpulse()
        {
            Quaternion startRotation = Quaternion.Euler(0f, 30f, 180f);
            ArenaFlightState state = State(startRotation);
            Vector3 start = state.Position;
            var tuning = new ArenaFlightTuning { rollRate = 180f };
            var held = new FlightInput(Vector2.right, 0.7f, true, false, 0f, true, false, true);
            const float dt = 0.1f;
            Vector3 velocity = study.Step(ref state, Vector2.right, 0f, 0.7f, tuning, Arena, held, dt);

            Assert.That(study.Released, Is.True);
            Assert.That(study.Seam.position.z, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(study.Seam.position.x, Is.GreaterThan(13f), "The exit must not recenter the ship.");
            Assert.That(study.Seam.position.y, Is.EqualTo(70f).Within(0.0001f));
            Assert.That(study.Seam.velocityChange, Is.LessThan(0.0001f));
            Quaternion expected = startRotation * Quaternion.Euler(0f, 0f, -18f);
            Assert.That(Quaternion.Angle(state.Rotation, expected), Is.LessThan(0.03f),
                "Held roll must act for the complete frame without a neutral gate or duplicate integration.");
            Assert.That(Vector3.Distance(state.Position, start), Is.EqualTo(3.1f).Within(0.0002f),
                "The throttle ramp must integrate once across the split frame.");
            Assert.That(velocity, Is.EqualTo((state.Position - start) / dt));
            Assert.That(study.Seam.steering, Is.EqualTo(held.Steering));
            Assert.That(study.Seam.rawThrottle, Is.EqualTo(held.Throttle));
            Assert.That(study.Seam.fire && study.Seam.lockHeld && study.Seam.empHeld, Is.True);
            Assert.That(study.Seam.arenaBoundarySampled, Is.True);
            Assert.That(study.ReleaseComparisonInvalid, Is.False);
            Assert.That(study.Seam.arenaBoundaryPositionCorrection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CourseDrivenReleaseReportsItsRealVelocityChangeInsteadOfBlending()
        {
            Set(study, "candidate", RailTravelMode.CourseDriven);
            ArenaFlightState state = State(Quaternion.Euler(0f, 60f, 180f));
            Quaternion attitude = state.Attitude;
            Vector3 start = state.Position;
            Vector3 velocity = study.Step(ref state, Vector2.zero, 0f, 0f,
                new ArenaFlightTuning(), Arena, default, 0.1f);

            Assert.That(study.Released, Is.True);
            Assert.That(study.Seam.railVelocity.z, Is.EqualTo(30f).Within(0.001f));
            Assert.That(study.Seam.arenaVelocity.z, Is.EqualTo(15f).Within(0.001f));
            Assert.That(study.Seam.velocityChange, Is.EqualTo(15f).Within(0.001f));
            Assert.That(state.Position.z, Is.EqualTo(11f).Within(0.0002f),
                "Travel should use 30 m/s until z=10, then the 15 m/s nose component for the remainder.");
            Assert.That(state.Position.x - start.x, Is.EqualTo(3f * Mathf.Sin(60f * Mathf.Deg2Rad)).Within(0.0002f));
            Assert.That(Quaternion.Angle(state.Attitude, attitude), Is.LessThan(0.03f));
            Assert.That(velocity.z, Is.EqualTo(20f).Within(0.003f));
        }

        [TestCase(RailTravelMode.NoseLed)]
        [TestCase(RailTravelMode.CourseDriven)]
        public void CrossingRetainsExistingShieldPaintedMissilePulseAndWeaponAuthority(RailTravelMode mode)
        {
            Set(study, "candidate", mode);
            Shield shieldState = shield.State;
            MissileLockState missileState = missiles.State;
            EmpChargeState empState = emp.State;
            var held = new FlightInput(new Vector2(0.2f, -0.3f), 0.4f, true, false, 0.2f, true, false, true);
            FlightInput accepted = weaponAuthority.Filter(held, true, true);
            SetAuto(flight, "CurrentInput", accepted, typeof(ShipFlight));
            ArenaFlightState state = State(Quaternion.identity);

            study.Step(ref state, held.Steering, held.Twist, held.Throttle, new ArenaFlightTuning(),
                Arena, accepted, 0.1f);

            Assert.That(study.Released, Is.True);
            Assert.That(actor.GetComponents<ShipFlight>(), Has.Length.EqualTo(1));
            Assert.That(actor.GetComponent<ShipFlight>(), Is.SameAs(flight));
            Assert.That(flight.enabled, Is.True);
            Assert.That(flight.CurrentInput, Is.EqualTo(accepted));
            Assert.That(shield.State, Is.SameAs(shieldState));
            Assert.That(shield.State.Current, Is.EqualTo(2));
            Assert.That(shield.State.ProtectionRemaining, Is.EqualTo(10f));
            Assert.That(missiles.State, Is.SameAs(missileState));
            Assert.That(missiles.State.Ammo, Is.EqualTo(3));
            Assert.That(missiles.State.IsPainting, Is.True);
            Assert.That(missiles.State.Locks, Is.EqualTo(new[] { 2 }));
            Assert.That(emp.State, Is.SameAs(empState));
            Assert.That(emp.State.Ready, Is.False);
            Assert.That(emp.Pulsing, Is.True);
            Assert.That((float)Get(emp, "age"), Is.EqualTo(0.8f));
            Assert.That(emp.Origin, Is.EqualTo(new Vector3(1f, 70f, 2f)));
            Assert.That(authority.Armed && authority.WeaponsAvailable, Is.True);
            FlightInput stillHeld = weaponAuthority.Filter(held, true, true);
            Assert.That(stillHeld.FireHeld && stillHeld.LockHeld && stillHeld.EmpHeld, Is.True,
                "A seam must not re-commission authority or add a release requirement.");
            Assert.That(stillHeld.LockCancelled, Is.False);
            Assert.That(study.Seam.shield, Is.EqualTo(2));
            Assert.That(study.Seam.missiles, Is.EqualTo(3));
            Assert.That(study.Seam.empReady, Is.False);
            Assert.That(study.Seam.empPulsing && study.Seam.armed, Is.True);
        }

        [TestCase(RailTravelMode.NoseLed)]
        [TestCase(RailTravelMode.CourseDriven)]
        public void PausedExactlyAtTheBoundaryDoesNotReleaseOrConsumeState(RailTravelMode mode)
        {
            Set(study, "candidate", mode);
            ArenaFlightState state = State(Quaternion.Euler(0f, 25f, 180f), 10f);
            ArenaFlightState before = state;
            Vector3 velocity = study.Step(ref state, Vector2.one, 1f, 1f,
                new ArenaFlightTuning(), Arena, default, 0f);
            Assert.That(state, Is.EqualTo(before));
            Assert.That(velocity, Is.EqualTo(Vector3.zero));
            Assert.That(study.Released, Is.False);
            Assert.That(study.Seam, Is.Null);
            Assert.That(missiles.State.Ammo, Is.EqualTo(3));
            Assert.That(emp.Pulsing, Is.True);
        }

        [Test]
        public void ResumingAtTheBoundaryUsesArenaLawForTheWholeRemainingFrame()
        {
            Set(study, "candidate", RailTravelMode.CourseDriven);
            ArenaFlightState state = State(Quaternion.Euler(0f, 60f, 0f), 10f);
            study.Step(ref state, Vector2.zero, 0f, 0f, new ArenaFlightTuning(), Arena, default, 0f);
            study.Step(ref state, Vector2.zero, 0f, 0f, new ArenaFlightTuning(), Arena, default, 0.1f);
            Assert.That(study.Released, Is.True);
            Assert.That(study.Seam.position.z, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(state.Position.z, Is.EqualTo(11.5f).Within(0.0001f),
                "A frame starting on the seam must not get another full frame of course-driven motion.");
        }

        [Test]
        public void DisabledStudyDoesNotExposeStaleCourseVelocityThroughShipFlight()
        {
            ArenaFlightState state = State(Quaternion.Euler(0f, 60f, 0f));
            Set(flight, "state", state);
            Set(flight, "study", study);
            Set(flight, "studyVelocity", new Vector3(20f, 0f, 30f));
            study.enabled = false;
            Assert.That(flight.Velocity, Is.EqualTo(state.Forward * state.Speed));
        }

        [Test]
        public void FarOutsideExitRecordsExistingArenaCorrectionWithoutChangingTheArenaLaw()
        {
            var limits = new ArenaLimits(Vector3.zero, 100f, 2f, 200f);
            var tuning = new ArenaFlightTuning();
            ArenaFlightState state = State(Quaternion.identity);
            state.Position.x = 500f;
            ArenaFlightState before = state;
            study.Step(ref state, Vector2.zero, 0f, 0f, tuning, limits, default, 0.1f);

            Assert.That(study.Seam.velocityChange, Is.Zero,
                "Identical nominal A velocity laws must not conceal a real boundary correction.");
            Assert.That(study.Seam.outsideEnvelope, Is.True);
            Assert.That(study.Seam.arenaBoundarySampled && study.Seam.arenaBoundaryCorrected, Is.True);
            Assert.That(study.ReleaseComparisonInvalid, Is.True);
            Assert.That(study.Seam.arenaBoundaryPositionCorrection.magnitude, Is.GreaterThan(390f));
            Assert.That(study.Seam.arenaBoundaryAngleDegrees, Is.GreaterThan(1f));
            Assert.That(new Vector2(state.Position.x, state.Position.z).magnitude, Is.EqualTo(100f).Within(0.001f));

            ArenaFlightState existingArena = before;
            existingArena.Position = study.Seam.position;
            ArenaFlightModel.StepFull(ref existingArena, Vector2.zero, 0f, 0f, tuning, limits,
                study.Seam.arenaSegmentSeconds);
            Assert.That(state.Position, Is.EqualTo(existingArena.Position));
            Assert.That(state.Attitude, Is.EqualTo(existingArena.Attitude));
            Assert.That(study.Seam.nextPosition - study.Seam.arenaUnboundedPosition,
                Is.EqualTo(study.Seam.arenaBoundaryPositionCorrection));
        }

        [Test]
        public void EndOfFrameExitDefersBoundaryEvidenceThroughPauseToFirstPositiveArenaStep()
        {
            var limits = new ArenaLimits(Vector3.zero, 100f, 2f, 200f);
            var tuning = new ArenaFlightTuning();
            ArenaFlightState state = State(Quaternion.identity, 7f);
            state.Position.x = 500f;
            study.Step(ref state, Vector2.zero, 0f, 0f, tuning, limits, default, 0.1f);
            Assert.That(study.Released, Is.True);
            Assert.That(study.Seam.position.z, Is.EqualTo(10f));
            Assert.That(study.Seam.arenaSegmentSeconds, Is.Zero);
            Assert.That(study.Seam.arenaBoundarySampled, Is.False);
            Assert.That(state.Position.x, Is.EqualTo(500f));

            study.Step(ref state, Vector2.one, 1f, 1f, tuning, limits, default, 0f);
            Assert.That(study.Seam.arenaBoundarySampled, Is.False);
            Assert.That(state.Position.x, Is.EqualTo(500f));

            study.Step(ref state, Vector2.zero, 0f, 0f, tuning, limits, default, 0.05f);
            Assert.That(study.Seam.arenaBoundarySampled && study.Seam.arenaBoundaryCorrected, Is.True);
            Assert.That(study.Seam.arenaSegmentSeconds, Is.EqualTo(0.05f));
            Assert.That(study.Seam.arenaBoundaryPositionCorrection.magnitude, Is.GreaterThan(390f));
            Vector3 firstMeasuredPosition = study.Seam.nextPosition;
            Vector3 firstCorrection = study.Seam.arenaBoundaryPositionCorrection;
            study.Step(ref state, Vector2.right, 0f, 0f, tuning, limits, default, 0.1f);
            Assert.That(study.Seam.nextPosition, Is.EqualTo(firstMeasuredPosition), "Retain the first step's evidence.");
            Assert.That(study.Seam.arenaBoundaryPositionCorrection, Is.EqualTo(firstCorrection));
        }

        [Test]
        public void ArenaFloorCorrectionInvalidatesAnOtherwiseInsideEnvelopeExit()
        {
            var limits = new ArenaLimits(Vector3.zero, 1000f, 80f, 200f);
            ArenaFlightState state = State(Quaternion.identity);
            study.Step(ref state, Vector2.zero, 0f, 0f, new ArenaFlightTuning(), limits, default, 0.1f);
            Assert.That(study.Seam.outsideEnvelope, Is.False);
            Assert.That(study.Seam.arenaBoundaryCorrected, Is.True);
            Assert.That(study.Seam.arenaBoundaryPositionCorrection.y, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(study.ReleaseComparisonInvalid, Is.True);
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);

        private static object Get(object target, string field) =>
            target.GetType().GetField(field, Private).GetValue(target);

        private static void SetAuto(object target, string property, object value, Type declaringType = null) =>
            (declaringType ?? target.GetType()).GetField("<" + property + ">k__BackingField", Private).SetValue(target, value);
    }
}

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
    /// <summary>The rail's end: the crossing itself, and the real flight component with initialized state holders.
    /// Objects stay inactive: no input devices, scene lifecycle, renderer or pools are used.</summary>
    public sealed class RailReleaseTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float ReleaseZ = 10f, HeadingLimit = 45f;
        private static readonly ArenaLimits Arena = new ArenaLimits(Vector3.zero, 10000f, -10000f, 10000f);
        private GameObject actor;
        private ShipFlight flight;
        private MasterArmInput authority;
        private WeaponAuthority weaponAuthority;
        private ShipShield shield;
        private LockOnWeapon missiles;
        private EmpWeapon emp;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Rail release test (inactive)");
            actor.SetActive(false);
            flight = actor.AddComponent<ShipFlight>();
            var source = actor.AddComponent<CombinedFlightInput>();
            var bounds = actor.AddComponent<ArenaBounds>();
            Set(bounds, "radius", 10000f);
            Set(flight, "arena", bounds);
            Set(flight, "inputSource", source);
            Set(flight, "railEndZ", ReleaseZ);
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

        private static bool Fly(ref ArenaFlightState state, Vector2 stick, float throttle, ArenaFlightTuning tuning,
            float dt, out Vector3 railEnd) =>
            RailFlightModel.FlyToRelease(ref state, stick, 0f, throttle, tuning, HeadingLimit, ReleaseZ, Arena, dt, out railEnd);

        [Test]
        public void ReleaseKeepsDisplacedInvertedPoseAndHeldRollWithoutVelocityImpulse()
        {
            Quaternion startRotation = Quaternion.Euler(0f, 30f, 180f);
            ArenaFlightState state = State(startRotation);
            Vector3 start = state.Position;
            const float dt = 0.1f;
            bool released = Fly(ref state, Vector2.right, 0.7f, new ArenaFlightTuning { rollRate = 180f }, dt, out Vector3 railEnd);

            Assert.That(released, Is.True);
            Assert.That(railEnd.z, Is.EqualTo(ReleaseZ).Within(0.0001f));
            Assert.That(railEnd.x, Is.GreaterThan(13f), "The exit must not recenter the ship.");
            Assert.That(railEnd.y, Is.EqualTo(70f).Within(0.0001f));
            Quaternion expected = startRotation * Quaternion.Euler(0f, 0f, -18f);
            Assert.That(Quaternion.Angle(state.Rotation, expected), Is.LessThan(0.03f),
                "Held roll must act for the complete frame without a neutral gate or duplicate integration.");
            Assert.That(Vector3.Distance(state.Position, start), Is.EqualTo(3.1f).Within(0.0002f),
                "The throttle ramp must integrate once across the split frame.");
        }

        [Test]
        public void AFrameShortOfThePlaneStaysOnTheRail()
        {
            ArenaFlightState state = State(Quaternion.identity, 5f);
            Assert.That(Fly(ref state, Vector2.zero, 0f, new ArenaFlightTuning(), 0.1f, out Vector3 railEnd), Is.False);
            Assert.That(railEnd, Is.EqualTo(state.Position));
            Assert.That(state.Position.z, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void CrossingRetainsExistingShieldPaintedMissilePulseAndWeaponAuthority()
        {
            Shield shieldState = shield.State;
            MissileLockState missileState = missiles.State;
            EmpChargeState empState = emp.State;
            var held = new FlightInput(new Vector2(0.2f, -0.3f), 0.4f, true, false, 0.2f, true, false, true);
            weaponAuthority.Filter(held, true, true);
            Set(flight, "state", State(Quaternion.identity));

            typeof(ShipFlight).GetMethod("Step", Private).Invoke(flight, new object[] { 0.1f });

            Assert.That(flight.RailReleased, Is.True);
            Assert.That(actor.GetComponents<ShipFlight>(), Has.Length.EqualTo(1));
            Assert.That(flight.enabled, Is.True);
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
                "A release must not re-commission authority or add a release requirement.");
            Assert.That(stillHeld.LockCancelled, Is.False);
        }

        [Test]
        public void PausedExactlyAtThePlaneDoesNotReleaseOrChangeTheState()
        {
            ArenaFlightState state = State(Quaternion.Euler(0f, 25f, 180f), ReleaseZ);
            ArenaFlightState before = state;
            Assert.That(Fly(ref state, Vector2.one, 1f, new ArenaFlightTuning(), 0f, out _), Is.False);
            Assert.That(state, Is.EqualTo(before));
        }

        [Test]
        public void ResumingOnThePlaneUsesTheArenaLawForTheWholeFrame()
        {
            ArenaFlightState state = State(Quaternion.Euler(0f, 60f, 0f), ReleaseZ);
            Fly(ref state, Vector2.zero, 0f, new ArenaFlightTuning(), 0f, out _);
            Assert.That(Fly(ref state, Vector2.zero, 0f, new ArenaFlightTuning(), 0.1f, out Vector3 railEnd), Is.True);
            Assert.That(railEnd.z, Is.EqualTo(ReleaseZ).Within(0.0001f));
            Assert.That(state.Position.z, Is.EqualTo(11.5f).Within(0.0001f),
                "The arena has no heading limit: a 60 degree nose keeps its own course.");
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);

        private static object Get(object target, string field) =>
            target.GetType().GetField(field, Private).GetValue(target);

        private static void SetAuto(object target, string property, object value) =>
            target.GetType().GetField("<" + property + ">k__BackingField", Private).SetValue(target, value);
    }
}

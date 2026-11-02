using GeometricFlight.Core;
using GeometricFlight.Input;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class OpeningTests
    {
        private static FlightInput Held(bool fire = true, bool missile = true, bool emp = true) =>
            new FlightInput(new Vector2(0.4f,-0.6f), 0.7f, fire, true, 0.3f, missile, false, emp);

        [Test]
        public void SafeInhibitsEveryWeaponWithoutAlteringFlightOrPause()
        {
            var authority = new WeaponAuthority();
            FlightInput result = authority.Filter(Held(), false, true);
            Assert.That(result.FireHeld || result.LockHeld || result.EmpHeld, Is.False);
            Assert.That(result.LockCancelled, Is.True);
            Assert.That(result.Steering, Is.EqualTo(Held().Steering));
            Assert.That(result.Throttle, Is.EqualTo(0.7f));
            Assert.That(result.Twist, Is.EqualTo(0.3f));
            Assert.That(result.PausePressed, Is.True);
            Assert.That(authority.RefusedThisFrame, Is.True);
            authority.Filter(Held(), false, true);
            Assert.That(authority.RefusedThisFrame, Is.False, "A held button must not repeat refusal feedback every frame.");
        }

        [Test]
        public void ArmingCannotReleaseHeldInputsAndEachChannelReleasesIndependently()
        {
            var authority = new WeaponAuthority();
            authority.Filter(Held(), false, true);
            FlightInput armed = authority.Filter(Held(), true, true);
            Assert.That(armed.FireHeld || armed.LockHeld || armed.EmpHeld, Is.False);
            authority.Filter(Held(false), true, true);
            FlightInput fire = authority.Filter(Held(), true, true);
            Assert.That(fire.FireHeld, Is.True);
            Assert.That(fire.LockHeld || fire.EmpHeld, Is.False);
            authority.Filter(default, true, true);
            FlightInput fresh = authority.Filter(Held(), true, true);
            Assert.That(fresh.FireHeld && fresh.LockHeld && fresh.EmpHeld, Is.True);
            Assert.That(fresh.LockCancelled, Is.False);
        }

        [Test]
        public void SafeCancelsPaintingInsteadOfFiringAReleaseVolley()
        {
            var authority = new WeaponAuthority();
            authority.Filter(default, true, true);
            Assert.That(authority.Filter(Held(), true, true).LockHeld, Is.True);
            FlightInput safe = authority.Filter(default, false, true);
            Assert.That(safe.LockHeld, Is.False);
            Assert.That(safe.LockCancelled, Is.True);
        }

        [Test]
        public void PauseAndResumeRequireFreshActionsEvenIfArmStaysOn()
        {
            var authority = new WeaponAuthority();
            authority.Filter(default, true, true);
            Assert.That(authority.Filter(Held(), true, true).FireHeld, Is.True);
            authority.Filter(Held(), true, false);
            FlightInput resumed = authority.Filter(Held(), true, true);
            Assert.That(resumed.FireHeld || resumed.LockHeld || resumed.EmpHeld, Is.False);
            authority.Filter(default, true, true);
            Assert.That(authority.Filter(Held(), true, true).EmpHeld, Is.True);
        }

        [Test]
        public void ExplicitDeviceHandoverAndSkipInvalidateHeldActions()
        {
            var authority = new WeaponAuthority();
            authority.Filter(default, true, true);
            authority.Filter(Held(), true, true);
            authority.RequireRelease();
            FlightInput handover = authority.Filter(Held(), true, true);
            Assert.That(handover.FireHeld || handover.LockHeld || handover.EmpHeld, Is.False);
            Assert.That(handover.LockCancelled, Is.True);
        }

        [Test]
        public void AnAlreadyArmedReturnNeedsNoSwitchCycle()
        {
            var authority = new WeaponAuthority();
            authority.Filter(default, true, true);
            FlightInput action = authority.Filter(Held(), true, true);
            Assert.That(action.FireHeld && action.LockHeld && action.EmpHeld, Is.True);
        }

        [Test]
        public void RingScoresOnceAndAMissedRingCannotBeClaimedLater()
        {
            var score = new RingScore();
            Assert.That(score.Cross(1, new Vector2(2,1), 3.2f), Is.True);
            Assert.That(score.Cross(1, Vector2.zero, 3.2f), Is.False);
            Assert.That(score.Cross(2, new Vector2(4,0), 3.2f), Is.False);
            Assert.That(score.Cross(2, Vector2.zero, 3.2f), Is.False);
            Assert.That(score.Hit, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SimulatorCarriesTheActualCockpitSwitchPosition(bool armed)
        {
            var simulator = new SimulatedLineTransport();
            simulator.SetArmed(armed);
            Assert.That(ControllerProtocol.TryParse(simulator.ReadLine(), out ControllerPacket packet), Is.True);
            Assert.That(packet.IsCockpit, Is.True);
            Assert.That(packet.Armed, Is.EqualTo(armed));
        }
    }
}

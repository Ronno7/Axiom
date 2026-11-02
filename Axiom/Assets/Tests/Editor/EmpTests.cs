using GeometricFlight.Combat;
using GeometricFlight.Input;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class EmpTests
    {
        [Test]
        public void HeldOnEntryDoesNotSpendCharge()
        {
            var state = new EmpChargeState();
            Assert.IsFalse(state.Step(true, true));
            Assert.IsTrue(state.Ready);
            state.Step(false, true);
            Assert.IsTrue(state.Step(true, true));
            Assert.IsFalse(state.Ready);
        }

        [Test]
        public void RepeatedPressesCannotSpendTwice()
        {
            var state = new EmpChargeState();
            state.Step(false, true);
            Assert.IsTrue(state.Step(true, true));
            Assert.IsFalse(state.Step(true, true));
            state.Step(false, true);
            Assert.IsFalse(state.Step(true, true));
        }

        [Test]
        public void PauseCannotQueueADeathBlast()
        {
            var state = new EmpChargeState();
            state.Step(false, true);
            Assert.IsFalse(state.Step(true, false));
            Assert.IsFalse(state.Step(true, true));
            Assert.IsTrue(state.Ready);
            state.Step(false, true);
            Assert.IsTrue(state.Step(true, true));
        }

        [Test]
        public void RespawnRequiresReleaseBeforeNewCharge()
        {
            var state = new EmpChargeState();
            state.Step(false, true);
            state.Step(true, true);
            state.Reset();
            Assert.IsTrue(state.Ready);
            Assert.IsFalse(state.Step(true, true));
            state.Step(false, true);
            Assert.IsTrue(state.Step(true, true));
        }

        [Test]
        public void FocusCancellationDoesNotSpendCharge()
        {
            var state = new EmpChargeState();
            state.Step(false, true);
            state.Cancel();
            Assert.IsFalse(state.Step(true, true));
            Assert.IsTrue(state.Ready);
        }

        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void EmpMergesBothInputDevices(bool keyboard, bool hardware, bool expected)
        {
            var keys = new FlightInput(Vector2.zero, 0, false, false, empHeld: keyboard);
            var controller = new ControllerState(true, Vector2.zero, 0, true, false, false, empHeld: hardware);
            Assert.AreEqual(expected, FlightInputMerge.Merge(keys, controller).EmpHeld);
        }

        [Test]
        public void DisconnectedHardwareCannotFireEmp()
        {
            var controller = new ControllerState(false, Vector2.zero, 0, true, false, false, empHeld: true);
            Assert.IsFalse(FlightInputMerge.Merge(default, controller).EmpHeld);
        }

        /// <summary>The tutorial's EMP prompt counts a drone as inside the pulse when its patrol centre plus its reach
        /// is within the radius. That holds only if no point of the patrol is farther than the reach.</summary>
        [TestCase(14f, 5f, 14f, 1, 2, 1, 0f)]
        [TestCase(84f, 21.84f, 84f, 1, 2, 3, 0f)]
        [TestCase(105f, 32.5f, 75f, 2, 3, 4, 45f)]
        public void DroneNeverLeavesItsReach(float ax, float ay, float az, int rx, int ry, int rz, float phase)
        {
            var amplitude = new Vector3(ax, ay, az);
            for (int i = 0; i <= 1000; i++)
                Assert.That(LissajousPatrol.Point(i / 1000f, amplitude, new Vector3Int(rx, ry, rz), phase).magnitude,
                    Is.LessThanOrEqualTo(amplitude.magnitude + 1e-3f));
        }

        [Test]
        public void OfflineCharge_CannotFireUntilReset()
        {
            var charge = new EmpChargeState();
            charge.TakeOffline();
            Assert.IsTrue(charge.Offline);
            Assert.IsFalse(charge.Ready);
            charge.Step(false, true);
            Assert.IsFalse(charge.Step(true, true));
            charge.Reset();
            Assert.IsFalse(charge.Offline);
            charge.Step(false, true);
            Assert.IsTrue(charge.Step(true, true));
        }
    }
}

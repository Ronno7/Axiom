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
    }
}

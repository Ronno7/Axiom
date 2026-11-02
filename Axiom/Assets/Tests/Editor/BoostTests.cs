using GeometricFlight.Player;
using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class BoostTests
    {
        private const float Dt = 1f / 60f;

        private static BoostTuning Tuning() => new BoostTuning { enabled = true };

        private static BoostState Armed()
        {
            BoostState s = BoostState.Full;
            ThrottleBoost.Step(ref s, 0f, Tuning(), Dt);
            return s;
        }

        private static void Hold(ref BoostState s, float throttle, float seconds, BoostTuning t)
        {
            for (float time = 0f; time < seconds - 1e-4f; time += Dt) ThrottleBoost.Step(ref s, throttle, t, Dt);
        }

        [Test]
        public void ForwardTravelReachesNormalFullAtTheGate()
        {
            BoostTuning t = Tuning();
            Assert.That(ThrottleBoost.Lever(0.375f, t), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(ThrottleBoost.Lever(0.75f, t), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ThrottleBoost.Lever(1f, t), Is.EqualTo(1f), "the gate adds no ordinary speed");
            Assert.That(ThrottleBoost.Lever(-0.5f, t), Is.EqualTo(-0.5f), "the back half is unchanged");
            Assert.That(ThrottleBoost.Lever(0.5f, new BoostTuning()), Is.EqualTo(0.5f), "disabled: unchanged");
        }

        [Test]
        public void CrossingTheGateFiresOneBurstThatSpendsTheCharge()
        {
            BoostTuning t = Tuning();
            BoostState s = Armed();
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.Ready));
            Assert.That(ThrottleBoost.Step(ref s, 1f, t, Dt), Is.True);
            Assert.That(s.Charge, Is.EqualTo(0f), "spent on activation");
            Hold(ref s, 1f, t.duration, t);
            Assert.That(s.Active, Is.False, "lasts at most the burst duration");
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.Charging));
        }

        [Test]
        public void PullingBackEndsTheBurstWithoutRefund()
        {
            BoostTuning t = Tuning();
            BoostState s = Armed();
            ThrottleBoost.Step(ref s, 1f, t, Dt);
            ThrottleBoost.Step(ref s, 0.5f, t, Dt);
            Assert.That(s.Active, Is.False);
            Assert.That(s.Charge, Is.EqualTo(0f));
        }

        [Test]
        public void ChargeRefillsAfterTheDelayAndNeverQueuesABurst()
        {
            BoostTuning t = Tuning();
            BoostState s = Armed();
            ThrottleBoost.Step(ref s, 1f, t, Dt);
            // Held in the gate through the burst, the delay and the whole refill.
            Hold(ref s, 1f, t.duration + t.rechargeDelay + t.rechargeTime + 0.2f, t);
            Assert.That(s.Charge, Is.EqualTo(1f));
            Assert.That(s.Active, Is.False, "a full charge with the lever forward does not fire");
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.PullBack));

            Hold(ref s, 0.7f, 0.5f, t);
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.PullBack), "0.7 is not a reset");
            ThrottleBoost.Step(ref s, 0.6f, t, Dt);
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.Ready));
            Assert.That(ThrottleBoost.Step(ref s, 0.8f, t, Dt), Is.True);
        }

        [Test]
        public void ChargeTakesTheConfiguredTimeToRefill()
        {
            BoostTuning t = Tuning();
            BoostState s = Armed();
            ThrottleBoost.Step(ref s, 1f, t, Dt);
            ThrottleBoost.Step(ref s, 0f, t, Dt); // Cancel: the delay starts now.
            Hold(ref s, 0f, t.rechargeDelay + t.rechargeTime * 0.5f, t);
            Assert.That(s.Charge, Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void AParkedLeverNeverFiresAfterAPauseOrHandOver()
        {
            BoostTuning t = Tuning();
            BoostState s = BoostState.Full;
            Assert.That(ThrottleBoost.Step(ref s, 1f, t, Dt), Is.False, "starts disarmed");

            s = Armed();
            ThrottleBoost.Step(ref s, 0.7f, t, Dt);
            ThrottleBoost.Step(ref s, 0.7f, t, 0f); // Paused.
            Assert.That(ThrottleBoost.Step(ref s, 1f, t, Dt), Is.False);

            s = BoostState.Full;
            ThrottleBoost.Step(ref s, 0f, t, 0f); // Held entry with the lever at cruise.
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.Ready), "not PULL BACK at cruise");
        }

        [Test]
        public void BurstSpeedComesFromTheFlightModelAndEasesBackAfter()
        {
            var tuning = new ArenaFlightTuning();
            tuning.boost.enabled = true;
            var state = new ArenaFlightState { Speed = tuning.cruiseSpeed, Boost = BoostState.Full };
            RailFlightModel.Step(ref state, Vector2.zero, 0f, 0f, tuning, 30f, Dt);
            for (int i = 0; i < 60; i++)
                RailFlightModel.Step(ref state, Vector2.zero, 0f, 1f, tuning, 30f, Dt);
            Assert.That(state.Speed, Is.EqualTo(tuning.boost.speed).Within(0.01f), "at boost speed within a second");
            for (int i = 0; i < 120; i++)
                RailFlightModel.Step(ref state, Vector2.zero, 0f, 1f, tuning, 30f, Dt);
            float full = FlightMovement.TargetSpeed(1f, tuning.cruiseSpeed, tuning.throttleMultipliers);
            Assert.That(state.Speed, Is.LessThan(tuning.boost.speed));
            Assert.That(state.Speed, Is.GreaterThanOrEqualTo(full), "eases back at normal acceleration");
        }

        [Test]
        public void ASpentBurstRetractsToNormalFullJustOutsideTheGate()
        {
            BoostTuning t = Tuning();
            float snapped = ArenaFlightModel.SnapCruise(ThrottleBoost.RetractSetpoint(t, 0.08f), 0.08f);
            Assert.That(snapped, Is.LessThan(t.gate));
            Assert.That(snapped, Is.GreaterThan(t.gate - 0.005f), "still reads FULL");
            Assert.That(ThrottleBoost.Lever(snapped, t), Is.GreaterThan(0.99f));

            BoostState s = Armed();
            ThrottleBoost.Step(ref s, 1f, t, Dt);
            Hold(ref s, 1f, t.duration, t);
            ThrottleBoost.Retracted(ref s);
            Hold(ref s, snapped, t.rechargeDelay + t.rechargeTime + 0.1f, t);
            Assert.That(ThrottleBoost.Status(s, t), Is.EqualTo(BoostStatus.Ready), "one push fires the next burst");
        }

        [Test]
        public void GaugeIsTheLeversTravel()
        {
            // 18–45 m/s with cruise 30, boost 70 beyond a 0.75 gate.
            Assert.That(ThrottleGauge.SpeedFraction(18f, 18f, 30f, 45f, 70f, 0.75f), Is.EqualTo(0f));
            Assert.That(ThrottleGauge.SpeedFraction(30f, 18f, 30f, 45f, 70f, 0.75f), Is.EqualTo(0.5f));
            Assert.That(ThrottleGauge.SpeedFraction(45f, 18f, 30f, 45f, 70f, 0.75f), Is.EqualTo(0.875f).Within(1e-5f));
            Assert.That(ThrottleGauge.SpeedFraction(70f, 18f, 30f, 45f, 70f, 0.75f), Is.EqualTo(1f));
            Assert.That(ThrottleGauge.SpeedFraction(45f, 18f, 30f, 45f, 45f, 1f), Is.EqualTo(1f), "no boost");
            Assert.That(ThrottleGauge.LeverFraction(0.75f), Is.EqualTo(ThrottleGauge.SpeedFraction(45f, 18f, 30f, 45f, 70f, 0.75f)),
                "the gate mark is where normal full speed is drawn");
        }
    }
}

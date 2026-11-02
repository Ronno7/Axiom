using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class RailFlightTests
    {
        private const float Dt = 1f / 60f;
        private static readonly ArenaLimits DistantLimits =
            new ArenaLimits(Vector3.zero, 1e9f, -1e9f, 1e9f);

        private static ArenaFlightState Start(bool full = true, Quaternion? attitude = null)
        {
            var state = new ArenaFlightState { Speed = 30f, Position = new Vector3(0f, 100f, 0f) };
            state.SetFullRotation(full);
            if (attitude.HasValue) state.Attitude = attitude.Value;
            return state;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnconstrainedNoseLedMatchesExistingHandlingAndIntegratedAcceleration(bool full)
        {
            var tuning = new ArenaFlightTuning();
            ArenaFlightState actual = Start(full);
            actual.Speed = 44f; // Reaches the 45 m/s target partway through this coarse frame.
            ArenaFlightState expected = actual;
            Vector3 before = actual.Position;
            var stick = new Vector2(0.1f, 0.1f);
            if (full) ArenaFlightModel.StepFull(ref expected, stick, 0.1f, 1f, tuning, DistantLimits, 0.2f);
            else ArenaFlightModel.Step(ref expected, stick, 0.1f, 1f, tuning, DistantLimits, 0.2f);
            Vector3 velocity = RailFlightModel.Step(ref actual, stick, 0.1f, 1f, tuning,
                45f, 0.2f);
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(Quaternion.Angle(actual.Rotation, expected.Rotation), Is.LessThan(0.01f));
            Assert.That(actual.Speed, Is.EqualTo(expected.Speed));
            Assert.That(velocity, Is.EqualTo((actual.Position - before) / 0.2f));
        }

        [TestCase(true, 0.016666667f)]
        [TestCase(true, 0.033333333f)]
        [TestCase(true, 0.1f)]
        [TestCase(false, 0.016666667f)]
        [TestCase(false, 0.033333333f)]
        [TestCase(false, 0.1f)]
        public void SustainedOutwardCommandsRespectConeAndKeepForwardProgress(bool full, float dt)
        {
            var tuning = new ArenaFlightTuning();
            ArenaFlightState state = Start(full);
            for (int i = 0; i < Mathf.RoundToInt(8f / dt); i++)
            {
                Vector3 velocity = RailFlightModel.Step(ref state, new Vector2(0.35f, 1f), 0.4f, 1f,
                    tuning, 45f, dt);
                Assert.That(Vector3.Angle(state.Forward, Vector3.forward), Is.LessThanOrEqualTo(45.02f));
                Assert.That(velocity.z, Is.GreaterThan(0f));
                Assert.That(state.Returning, Is.False, "No arena return assistance belongs in the rail law.");
            }
        }

        [Test]
        public void RollAtTheConeEdgeRemainsAFullRollAndNeutralDoesNotRecenter()
        {
            var tuning = new ArenaFlightTuning { rollRate = 180f };
            Quaternion initial = Quaternion.Euler(0f, 45f, 0f);
            ArenaFlightState state = Start(true, initial);
            Vector3 nose = state.Forward;
            for (int i = 0; i < 120; i++)
                RailFlightModel.Step(ref state, Vector2.right, 0f, 0f, tuning, 45f, Dt);
            Assert.That(Quaternion.Angle(state.Attitude, initial), Is.LessThan(0.1f));
            Assert.That(Vector3.Angle(state.Forward, nose), Is.LessThan(0.03f));
            Quaternion beforeNeutral = state.Attitude;
            for (int i = 0; i < 60; i++)
                RailFlightModel.Step(ref state, Vector2.zero, 0f, 0f, tuning, 45f, Dt);
            Assert.That(Quaternion.Angle(state.Attitude, beforeNeutral), Is.LessThan(0.03f));
        }

        [Test]
        public void TangentialPitchAtSideOfConeStillMovesTheNose()
        {
            ArenaFlightState state = Start(true, Quaternion.Euler(0f, 45f, 0f));
            RailFlightModel.Step(ref state, Vector2.up, 0f, 0f, new ArenaFlightTuning(),
                45f, 0.1f);
            Assert.That(state.Forward.y, Is.GreaterThan(0.1f), "Rejecting a whole rotation would freeze this allowed motion.");
            Assert.That(Vector3.Angle(state.Forward, Vector3.forward), Is.EqualTo(45f).Within(0.02f));
        }

        [Test]
        public void InwardSteeringLeavesTheLimitImmediately()
        {
            ArenaFlightState state = Start(true, Quaternion.Euler(-45f, 0f, 0f));
            RailFlightModel.Step(ref state, Vector2.down, 0f, 0f, new ArenaFlightTuning(),
                45f, 0.1f);
            Assert.That(Vector3.Angle(state.Forward, Vector3.forward), Is.EqualTo(35f).Within(0.02f));
        }

        [Test]
        public void InvertedPitchUsesLocalAxesAndDoesNotLevel()
        {
            ArenaFlightState state = Start(true, Quaternion.Euler(0f, 0f, 180f));
            RailFlightModel.Step(ref state, Vector2.up, 0f, 0f, new ArenaFlightTuning(),
                45f, 0.1f);
            Assert.That(state.Forward.y, Is.LessThan(-0.1f));
            Assert.That((state.Attitude * Vector3.up).y, Is.LessThan(-0.9f));
        }

        [Test]
        public void BankAndTurnRetainsBankLagWhenYawHitsTheLimit()
        {
            var tuning = new ArenaFlightTuning();
            ArenaFlightState state = Start(false);
            state.Yaw = 45f;
            state.Bank = 25f;
            ArenaFlightState expected = state;
            ArenaFlightModel.Step(ref expected, Vector2.right, 0f, 0f, tuning, DistantLimits, 0.1f);
            RailFlightModel.Step(ref state, Vector2.right, 0f, 0f, tuning, 45f, 0.1f);
            Assert.That(state.Bank, Is.EqualTo(expected.Bank));
            Assert.That(state.Yaw, Is.EqualTo(45f).Within(0.02f));
            Assert.That(Quaternion.Angle(state.Attitude, state.Rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void InvalidImportedHeadingIsNotSnappedIntoTheCone()
        {
            Quaternion initial = Quaternion.Euler(-65f, 0f, 20f);
            ArenaFlightState state = Start(true, initial);
            RailFlightModel.Step(ref state, Vector2.zero, 0f, 0f, new ArenaFlightTuning(),
                45f, Dt);
            Assert.That(Quaternion.Angle(state.Attitude, initial), Is.LessThan(0.02f));
        }

        [Test]
        public void CoarseHalfLoopRequestStillProducesAForwardHeading()
        {
            ArenaFlightState state = Start();
            Vector3 velocity = RailFlightModel.Step(ref state, Vector2.up, 0f, 0f, new ArenaFlightTuning(),
                45f, 1.8f);
            Assert.That(Vector3.Angle(state.Forward, Vector3.forward), Is.EqualTo(45f).Within(0.02f));
            Assert.That(velocity.z, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(state.Attitude.w), Is.False);
        }

        [Test]
        public void NonPositiveTimeDoesNotMutateAnyState()
        {
            ArenaFlightState state = Start(true, Quaternion.Euler(10f, 20f, 30f));
            state.Returning = true;
            state.Stalled = true;
            ArenaFlightState before = state;
            foreach (float dt in new[] { 0f, -0.1f })
            {
                Vector3 velocity = RailFlightModel.Step(ref state, Vector2.one, 1f, 1f,
                    new ArenaFlightTuning(), 45f, dt);
                Assert.That(state, Is.EqualTo(before));
                Assert.That(velocity, Is.EqualTo(Vector3.zero));
            }
        }
    }
}

using GeometricFlight.Presentation;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class MenuDepartureTimelineTests
    {
        [TestCase(8f, false)]
        [TestCase(10f, false)]
        [TestCase(12f, false)]
        [TestCase(3f, true)]
        public void TravelIsForwardAndMatchesBetweenTreatments(float duration, bool returning)
        {
            float previous = -1f;
            for (int frame = 0; frame <= (duration + 3f) * 60f; frame++)
            {
                float elapsed = frame / 60f;
                var a = At(elapsed, duration, returning, DepartureTreatment.FlyThrough);
                var b = At(elapsed, duration, returning, DepartureTreatment.SignalTransfer);
                Assert.That(a.Distance, Is.GreaterThanOrEqualTo(previous), "distance at " + elapsed);
                Assert.That(a.Speed, Is.GreaterThan(0f), "speed at " + elapsed);
                Assert.That(a.FovSurge, Is.InRange(0f, MenuDepartureTimeline.MaxFovSurge));
                Assert.That(b.Distance, Is.EqualTo(a.Distance));
                Assert.That(b.Speed, Is.EqualTo(a.Speed));
                Assert.That(b.FovSurge, Is.EqualTo(a.FovSurge));
                Assert.That(a.Occlusion, Is.Zero);
                Assert.That(a.TransferReady, Is.False);
                previous = a.Distance;
            }
        }

        [TestCase(8f, false)]
        [TestCase(10f, false)]
        [TestCase(12f, false)]
        [TestCase(3f, true)]
        public void DestinationAndVelocityContinueThroughArrival(float duration, bool returning)
        {
            var start = At(0f, duration, returning);
            var before = At(duration - 0.001f, duration, returning);
            var arrival = At(duration, duration, returning);
            var after = At(duration + 3f, duration, returning);
            Assert.That(start.Distance, Is.Zero);
            Assert.That(start.Speed, Is.EqualTo(MenuDepartureTimeline.IdleSpeed));
            Assert.That(start.FovSurge, Is.Zero);
            Assert.That(before.Arrived, Is.False);
            Assert.That(arrival.Arrived, Is.True);
            Assert.That(arrival.Distance, Is.EqualTo(MenuDepartureTimeline.TotalDistance(returning)));
            Assert.That(arrival.Speed, Is.EqualTo(MenuDepartureTimeline.ArrivalSpeed));
            Assert.That(before.Speed, Is.EqualTo(arrival.Speed).Within(0.04f));
            Assert.That((arrival.Distance - before.Distance) / 0.001f, Is.EqualTo(arrival.Speed).Within(0.1f));
            Assert.That(after.Distance - arrival.Distance, Is.EqualTo(90f).Within(0.001f));
            Assert.That(after.Speed, Is.EqualTo(arrival.Speed));
            Assert.That(arrival.FovSurge, Is.Zero);
        }

        [TestCase(30)]
        [TestCase(60)]
        public void SignalTransferHasCoveredFrameBeforeCommitAtBothFrameRates(int fps)
        {
            bool found = false;
            float previousCover = 0f;
            for (int frame = 0; frame <= 10 * fps; frame++)
            {
                var sample = At(frame / (float)fps, 10f, false, DepartureTreatment.SignalTransfer);
                Assert.That(sample.Occlusion, Is.InRange(0f, 1f));
                if (sample.TransferReady)
                {
                    Assert.That(sample.Occlusion, Is.EqualTo(1f));
                    Assert.That(previousCover, Is.EqualTo(1f));
                    found = true;
                }
                previousCover = sample.Occlusion;
            }
            Assert.That(found, Is.True, "the transfer window must be observable at " + fps + " fps");
        }

        [Test]
        public void SamplingHistoryDoesNotChangeTheThirtyAndSixtyFpsPath()
        {
            for (int frame = 0; frame <= 13 * 30; frame++)
            {
                float atThirty = frame / 30f;
                float atSixty = (frame * 2) / 60f;
                var a = At(atThirty, 10f, false, DepartureTreatment.SignalTransfer);
                At((frame * 2 - 1) / 60f, 10f, false, DepartureTreatment.SignalTransfer);
                var b = At(atSixty, 10f, false, DepartureTreatment.SignalTransfer);
                Assert.That(b.Distance, Is.EqualTo(a.Distance));
                Assert.That(b.Speed, Is.EqualTo(a.Speed));
                Assert.That(b.Occlusion, Is.EqualTo(a.Occlusion));
                Assert.That(b.TransferReady, Is.EqualTo(a.TransferReady));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReducedAnimationTransfersOnlyUnderCoverAndHasNoSurge(bool returning)
        {
            float lastDistance = 0f;
            bool jumped = false;
            for (int frame = 0; frame <= 60; frame++)
            {
                float elapsed = frame / 60f;
                var a = MenuDepartureTimeline.Sample(elapsed, 12f, returning, true, DepartureTreatment.FlyThrough);
                var b = MenuDepartureTimeline.Sample(elapsed, 8f, returning, true, DepartureTreatment.SignalTransfer);
                Assert.That(a.FovSurge, Is.Zero);
                Assert.That(b.Distance, Is.EqualTo(a.Distance));
                Assert.That(b.Occlusion, Is.EqualTo(a.Occlusion));
                if (a.Distance != lastDistance)
                {
                    Assert.That(a.Occlusion, Is.EqualTo(1f));
                    Assert.That(a.TransferReady, Is.True);
                    jumped = true;
                }
                lastDistance = a.Distance;
            }
            Assert.That(jumped, Is.True);
            var after = MenuDepartureTimeline.Sample(2f, 12f, returning, true, DepartureTreatment.FlyThrough);
            Assert.That(after.Arrived, Is.True);
            Assert.That(after.Distance, Is.EqualTo(MenuDepartureTimeline.TotalDistance(returning) + 30f));
            Assert.That(after.Speed, Is.EqualTo(30f));
        }

        [Test]
        public void InvalidClockAndDurationInputsStayFinite()
        {
            foreach (float time in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -5f, float.MaxValue })
                foreach (float duration in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -5f, 0f, float.MaxValue })
                {
                    var sample = At(time, duration, false, DepartureTreatment.SignalTransfer);
                    Assert.That(float.IsNaN(sample.Distance) || float.IsInfinity(sample.Distance), Is.False);
                    Assert.That(float.IsNaN(sample.Speed) || float.IsInfinity(sample.Speed), Is.False);
                    Assert.That(sample.Distance, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(sample.Speed, Is.GreaterThanOrEqualTo(0f));
                }
        }

        [TestCase(false, 6f)]
        [TestCase(false, 14f)]
        [TestCase(true, 2f)]
        [TestCase(true, 4f)]
        public void ClampedDurationBoundsRemainMonotonic(bool returning, float duration)
        {
            float last = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                var sample = At(duration * i / 1000f, duration, returning);
                Assert.That(sample.Distance, Is.GreaterThanOrEqualTo(last));
                Assert.That(sample.Speed, Is.GreaterThan(0f));
                last = sample.Distance;
            }
        }

        private static DepartureSample At(float elapsed, float duration, bool returning,
            DepartureTreatment treatment = DepartureTreatment.FlyThrough) =>
            MenuDepartureTimeline.Sample(elapsed, duration, returning, false, treatment);
    }
}

using GeometricFlight.Core;
using GeometricFlight.Player;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    /// <summary>The death sequence's timeline: the crash held until the cut, the hull rebuilt
    /// until control, the shield refilling with it.</summary>
    public sealed class DeathSequenceTests
    {
        [Test]
        public void TheRetryTakesTwoAndAHalfSecondsWithOneCut()
        {
            Assert.That(ShipRebuild.CutSeconds, Is.EqualTo(0.75f));
            Assert.That(ShipRebuild.ControlSeconds, Is.EqualTo(2.5f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0f)]
        [TestCase(0.75f, 0f)]
        [TestCase(1.625f, 0.5f)]
        [TestCase(2.5f, 1f)]
        [TestCase(4f, 1f)]
        public void TheBuildRunsFromTheCutToControl(float elapsed, float expected)
        {
            Assert.That(ShipRebuild.BuildAt(elapsed), Is.EqualTo(expected).Within(1e-4f));
        }

        [TestCase(0f, 0)]
        [TestCase(0.3f, 0)]
        [TestCase(1f / 3f, 1)]
        [TestCase(0.5f, 1)]
        [TestCase(2f / 3f, 2)]
        [TestCase(0.99f, 2)]
        [TestCase(1f, 3)]
        public void TheShieldComesBackOneSegmentAtATime(float build, int segments)
        {
            Assert.That(ShipRebuild.SegmentsAt(build, 3), Is.EqualTo(segments));
        }

        [Test]
        public void ARefillSetsTheSegmentsAndControlStartsProtected()
        {
            var shield = new Shield(3, 1.2f);
            shield.Deplete();
            shield.Refill(2);
            Assert.That(shield.Current, Is.EqualTo(2));
            Assert.That(shield.IsDepleted, Is.False);
            shield.Refill(5);
            Assert.That(shield.Current, Is.EqualTo(3), "never above the maximum");
            shield.Protect();
            Assert.That(shield.IsProtected, "control starts inside the protection window");
            Assert.That(shield.TryHit(), Is.False, "a hit in the first moment of flight is ignored");
            shield.Tick(1.3f);
            Assert.That(shield.TryHit(), Is.True);
        }
    }
}

using GeometricFlight.Player;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class ShieldTests
    {
        [Test]
        public void HitRemovesOneSegmentThenProtects()
        {
            var shield = new Shield(3, 1f);
            Assert.That(shield.TryHit(), Is.True);
            Assert.That(shield.Current, Is.EqualTo(2));
            Assert.That(shield.TryHit(), Is.False, "second hit during protection");
            Assert.That(shield.Current, Is.EqualTo(2));
        }

        [Test]
        public void ProtectionExpiresWithTime()
        {
            var shield = new Shield(3, 1f);
            shield.TryHit();
            shield.Tick(0.6f);
            Assert.That(shield.TryHit(), Is.False);
            shield.Tick(0.5f);
            Assert.That(shield.TryHit(), Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void PausedTimeDoesNotExpireProtection(float deltaTime)
        {
            var shield = new Shield(3, 1f);
            shield.TryHit();
            for (int i = 0; i < 100; i++) shield.Tick(deltaTime);
            Assert.That(shield.IsProtected, Is.True);
        }

        [Test]
        public void DepletesOnceAndIgnoresLaterHits()
        {
            var shield = new Shield(2, 0.5f);
            shield.TryHit();
            shield.Tick(1f);
            Assert.That(shield.TryHit(), Is.True);
            Assert.That(shield.IsDepleted, Is.True);
            Assert.That(shield.IsProtected, Is.False);
            Assert.That(shield.TryHit(), Is.False);
            Assert.That(shield.Current, Is.Zero);
        }
    }
}

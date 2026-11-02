using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ShieldTests
    {
        [Test]
        public void ARestoreChangesTheCountWithoutCountingAsAHit()
        {
            var go = new GameObject("TestShip") { hideFlags = HideFlags.DontSave };
            try
            {
                var shield = go.AddComponent<ShipShield>();
                // Awake does not run for EditMode AddComponent without ExecuteAlways; invoke it.
                typeof(ShipShield).GetMethod("Awake",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(shield, null);
                int changes = 0, hits = 0;
                shield.Changed += _ => changes++;
                shield.Hit += _ => hits++;
                Assert.That(shield.ApplyHit(), Is.True);
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(hits, Is.EqualTo(1));
                shield.Restore();
                Assert.That(changes, Is.EqualTo(2), "the gauge still hears about a restore");
                Assert.That(hits, Is.EqualTo(1), "damage feedback doesn't");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

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

using GeometricFlight.Core;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class RunRecordTests
    {
        private static RunResults Results(bool completed, int targets = 0, int shots = 0, int hits = 0, int shield = 3) =>
            new RunResults(completed, 42f, targets, 3, shots, hits, shield, 3);

        [Test]
        public void CountsTargetsWhileTheRunIsOn()
        {
            var record = new RunRecord();
            record.CountTarget();
            record.CountTarget();
            Assert.That(record.TargetsDestroyed, Is.EqualTo(2));
            Assert.That(record.Finished, Is.False);
        }

        [Test]
        public void FirstEndingIsFinal()
        {
            var record = new RunRecord();
            Assert.That(record.TryFinish(Results(completed: true, shield: 2)), Is.True);
            Assert.That(record.TryFinish(Results(completed: false, shield: 0)), Is.False, "a late failure after completing");
            Assert.That(record.Results.Completed, Is.True);
            Assert.That(record.Results.ShieldRemaining, Is.EqualTo(2));
        }

        [Test]
        public void LateTargetAfterTheEndingChangesNothing()
        {
            var record = new RunRecord();
            record.CountTarget();
            record.TryFinish(Results(completed: false, targets: record.TargetsDestroyed));
            record.CountTarget(); // a shot already in flight breaks a target after the ship is destroyed
            Assert.That(record.TargetsDestroyed, Is.EqualTo(1));
            Assert.That(record.Results.TargetsDestroyed, Is.EqualTo(1));
        }

        [Test]
        public void AccuracyIsHitsPerShotAndZeroWithoutShots()
        {
            Assert.That(Results(true, shots: 8, hits: 6).Accuracy, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(Results(true).Accuracy, Is.Zero);
        }
    }
}

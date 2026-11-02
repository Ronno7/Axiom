using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ContactTests
    {
        [Test]
        public void LethalContactDepletesEvenDuringProtection()
        {
            var shield = new Shield(3, 1.2f);
            Assert.That(shield.TryHit(), Is.True);
            Assert.That(shield.IsProtected, Is.True);
            Assert.That(shield.TryHit(), Is.False, "an obstacle hit is ignored while protected");
            Assert.That(shield.Deplete(), Is.True, "a wall is not");
            Assert.That(shield.IsDepleted, Is.True);
            Assert.That(shield.Deplete(), Is.False, "depletes exactly once");
        }

        [Test]
        public void PacingCheckFindsManoeuvresThatOnlyWorkSlowly()
        {
            var tuning = new GeometricFlight.Player.ArenaFlightTuning();
            var bounds = new Vector2(32f, 34f);
            var start = new Vector3(0f, 16f, 0f);
            var easy = new[] { new GeometricFlight.Core.RailCourseCheck.Gate("easy", new Vector3(10f, 16f, 200f), 8f) };
            // A quick jink: across 30 m to one side, then 30 m back within 70 m of travel.
            var jink = new[]
            {
                new GeometricFlight.Core.RailCourseCheck.Gate("a", new Vector3(0f, 16f, 100f), 0f, 4f),
                new GeometricFlight.Core.RailCourseCheck.Gate("b", new Vector3(-15f, 16f, 135f), 0f, 4f),
                new GeometricFlight.Core.RailCourseCheck.Gate("c", new Vector3(15f, 16f, 170f), 0f, 4f),
            };
            foreach (float throttle in new[] { -1f, 0f, 1f })
                Assert.That(GeometricFlight.Core.RailCourseCheck.Fly(easy, throttle, tuning, 45f, start, bounds).Passed,
                    Is.EqualTo(1), $"a gentle ring at throttle {throttle}");
            var slow = GeometricFlight.Core.RailCourseCheck.Fly(jink, -1f, tuning, 45f, start, bounds);
            var full = GeometricFlight.Core.RailCourseCheck.Fly(jink, 1f, tuning, 45f, start, bounds);
            Assert.That(slow.Passed, Is.GreaterThan(full.Passed), "the jink rewards slowing down");
        }

        [Test]
        public void TurretsLeadAShipFlyingStraightAtThem()
        {
            // A ship 100 m away closing at 30 m/s; a 16 m/s shot meets it after about 2.17 s.
            float t = GeometricFlight.Combat.WeaponRules.InterceptTime(new Vector3(0f, 0f, -100f), new Vector3(0f, 0f, 30f), 16f);
            Assert.That(t, Is.EqualTo(100f / 46f).Within(1e-3f));
            // Sideways at 30 m/s, 60 m off: a 16 m/s shot can never catch it.
            Assert.That(GeometricFlight.Combat.WeaponRules.InterceptTime(new Vector3(60f, 0f, 0f), new Vector3(30f, 0f, 0f), 16f), Is.EqualTo(-1f));
        }

        [Test]
        public void ATimedGateOnlyLetsAFastShipThrough()
        {
            // The tutorial's iris: 160 m from trigger to iris, about 4.7 s to get through (34 m/s or faster).
            var tuning = new GeometricFlight.Player.ArenaFlightTuning();
            var iris = new[] { new GeometricFlight.Core.RailCourseCheck.Gate("iris", new Vector3(0f, 16f, 300f), 2f, 0f,
                160f, 160f / 34f) };
            var bounds = new Vector2(32f, 34f);
            var start = new Vector3(0f, 16f, 0f);
            Assert.That(GeometricFlight.Core.RailCourseCheck.Fly(iris, 0f, tuning, 45f, start, bounds).Passed, Is.Zero,
                "cruise is too slow");
            Assert.That(GeometricFlight.Core.RailCourseCheck.Fly(iris, 1f, tuning, 45f, start, bounds).Passed, Is.EqualTo(1),
                "full throttle makes it");
        }

        [Test]
        public void ACheckpointRetryRestoresTheScoreAsItWasThere()
        {
            var score = new GeometricFlight.Core.RingScore();
            score.Cross(0, Vector2.zero, 10f);
            var atCheckpoint = score.Save();
            score.Cross(1, Vector2.zero, 10f);
            score.Cross(2, new Vector2(20f, 0f), 10f);
            score.Restore(atCheckpoint);
            Assert.That(score.Hit, Is.EqualTo(1));
            Assert.That(score.Cross(0, Vector2.zero, 10f), Is.False, "a ring passed before the checkpoint stays spent");
            Assert.That(score.Cross(1, Vector2.zero, 10f), Is.True, "rings after it can be scored again");
            Assert.That(score.Cross(2, Vector2.zero, 10f), Is.True, "including ones missed before the retry");
        }

        [Test]
        public void ChaseCameraSettlesBehindAtSpeedButNeverBeyondItsMaximumLag()
        {
            Vector3 slow = ChaseCamera.SteadyTrail(new Vector3(0f, 0f, 10f), 0.15f, 2f);
            Assert.That(slow.z, Is.EqualTo(-1.5f).Within(1e-5f));
            Vector3 cruise = ChaseCamera.SteadyTrail(new Vector3(0f, 0f, 30f), 0.15f, 2f);
            Assert.That(cruise.magnitude, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(cruise.z, Is.LessThan(0f), "the camera trails behind the motion");
        }
    }
}

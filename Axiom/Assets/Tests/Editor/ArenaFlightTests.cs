using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ArenaFlightTests
    {
        private const float Dt = 1f / 60f;
        private static readonly ArenaLimits Arena = new ArenaLimits(Vector3.zero, 160f, 2f, 100f);

        private static ArenaFlightState Start(ArenaFlightTuning t, float altitude = 30f) =>
            new ArenaFlightState { Position = new Vector3(0f, altitude, 0f), Speed = t.cruiseSpeed };

        private static ArenaFlightState Fly(ArenaFlightTuning t, Vector2 stick, float twist, float throttle,
            float seconds, ArenaFlightState? from = null)
        {
            ArenaFlightState s = from ?? Start(t);
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) ArenaFlightModel.Fly(ref s, stick, twist, throttle, t, Arena, Dt);
            return s;
        }

        [Test]
        public void CentredStickFliesStraightAtCruise()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Fly(t, Vector2.zero, 0f, 0f, 2f);
            Assert.That(s.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(s.Position.y, Is.EqualTo(30f).Within(0.001f));
            Assert.That(s.Position.z, Is.EqualTo(t.cruiseSpeed * 2f).Within(0.6f));
        }

        [Test]
        public void TurnIsFastestOnTheDetent()
        {
            var t = new ArenaFlightTuning();
            float Pulled(float throttle) => Vector3.Angle(Fly(t, Vector2.up, 0f, throttle, 0.5f).Forward, Vector3.forward);
            Assert.That(Pulled(0f), Is.GreaterThan(Pulled(-1f)));
            Assert.That(Pulled(0f), Is.GreaterThan(Pulled(1f)));
        }

        [Test]
        public void BrakeEndGivesTheSmallestCircle()
        {
            var t = new ArenaFlightTuning();
            float speedMin = t.cruiseSpeed * t.throttleMultipliers.x;
            float speedMax = t.cruiseSpeed * t.throttleMultipliers.y;
            float rMin = ArenaFlightModel.TurnRadius(speedMin, t.loopRate * t.turnAtThrottleEnds.x);
            float rCruise = ArenaFlightModel.TurnRadius(t.cruiseSpeed, t.loopRate);
            float rMax = ArenaFlightModel.TurnRadius(speedMax, t.loopRate * t.turnAtThrottleEnds.y);
            Assert.That(rMin, Is.LessThan(rCruise));
            Assert.That(rCruise, Is.LessThan(rMax));
        }

        [Test]
        public void TwistYawsWithoutBanking()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Fly(t, Vector2.zero, 1f, 0f, 1f);
            Assert.That(s.Bank, Is.EqualTo(0f).Within(0.001f));
            Assert.That(s.Yaw, Is.EqualTo(t.rudderRate).Within(1f));
        }

        [Test]
        public void ANewStateFacesForwardLevel()
        {
            var s = new ArenaFlightState();
            Assert.That(s.Attitude, Is.EqualTo(Quaternion.identity));
            Assert.That(s.Forward, Is.EqualTo(Vector3.forward));
            Assert.That(s.Yaw, Is.Zero);
            Assert.That(s.Pitch, Is.Zero);
            Assert.That(s.Bank, Is.Zero);
        }

        [Test]
        public void EdgeTurnsAShipBackBeforeTheWall()
        {
            var t = new ArenaFlightTuning();
            var s = new ArenaFlightState { Position = new Vector3(0f, 30f, 120f), Speed = t.cruiseSpeed };
            bool returned = false;
            float furthest = 0f;
            for (int i = 0; i < 60 * 15; i++)
            {
                ArenaFlightModel.Fly(ref s, Vector2.zero, 0f, 0f, t, Arena, Dt);
                returned |= s.Returning;
                furthest = Mathf.Max(furthest, new Vector2(s.Position.x, s.Position.z).magnitude);
            }
            Assert.That(returned, Is.True);
            Assert.That(furthest, Is.LessThan(Arena.Radius), "the auto-turn should act before the hard limit");
        }

        [Test]
        public void ZeroTimeChangesNothing()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Start(t);
            ArenaFlightState before = s;
            ArenaFlightModel.Fly(ref s, Vector2.one, 1f, 1f, t, Arena, 0f);
            Assert.That(s, Is.EqualTo(before));
        }

        [Test]
        public void HeldBackFliesALoop()
        {
            var t = new ArenaFlightTuning { gravity = 0f }; // geometry only: no speed lost on the way up
            ArenaFlightState s = Start(t, 50f);
            ArenaFlightState half = Fly(t, Vector2.up, 0f, 0f, 180f / t.loopRate, s);
            Assert.That(Vector3.Dot(half.Attitude * Vector3.up, Vector3.up), Is.LessThan(-0.95f), "inverted at the top");
            Assert.That(half.Position.y, Is.GreaterThan(50f + 2f * ArenaFlightModel.TurnRadius(t.cruiseSpeed, t.loopRate) - 3f));
            ArenaFlightState full = Fly(t, Vector2.up, 0f, 0f, 180f / t.loopRate, half);
            Assert.That(Vector3.Angle(full.Forward, Vector3.forward), Is.LessThan(2f), "back to the starting heading");
            Assert.That(full.Position.y, Is.EqualTo(50f).Within(1.5f));
        }

        [Test]
        public void HeldRightFliesABarrelRoll()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Fly(t, Vector2.right, 0f, 0f, 360f / t.rollRate, Start(t, 50f));
            Assert.That(Quaternion.Angle(s.Attitude, Quaternion.identity), Is.LessThan(2f));
            Assert.That(s.Position.y, Is.EqualTo(50f).Within(0.01f), "a roll alone doesn't change height");
        }

        [Test]
        public void NeverLeavesTheArena()
        {
            var t = new ArenaFlightTuning();
            // Full throttle, looping, rolling and diving, with the twist fighting the return turn.
            foreach (Vector2 stick in new[] { Vector2.zero, Vector2.up, Vector2.down, new Vector2(1f, 0.4f), new Vector2(-0.2f, -1f) })
            {
                ArenaFlightState s = Start(t);
                for (int i = 0; i < 60 * 40; i++)
                {
                    ArenaFlightModel.Fly(ref s, stick, 1f, 1f, t, Arena, Dt);
                    var flat = new Vector2(s.Position.x, s.Position.z);
                    Assert.That(flat.magnitude, Is.LessThanOrEqualTo(Arena.Radius + 0.001f));
                    Assert.That(s.Position.y, Is.InRange(Arena.MinAltitude - 0.001f, Arena.Ceiling + 0.001f));
                }
            }
        }

        [Test]
        public void ClimbingBleedsSpeedIntoAStallAndTheNoseFalls()
        {
            var t = new ArenaFlightTuning { gravity = 15f };
            ArenaFlightState s = Start(t, 10f);
            s.Attitude = Quaternion.Euler(-80f, 0f, 0f);
            bool stalled = false;
            float slowest = s.Speed, lowestPitch = 90f;
            for (int i = 0; i < 60 * 6; i++)
            {
                ArenaFlightModel.Fly(ref s, Vector2.zero, 0f, 0f, t, Arena, Dt);
                stalled |= s.Stalled;
                slowest = Mathf.Min(slowest, s.Speed);
                lowestPitch = Mathf.Min(lowestPitch, s.Pitch);
            }
            Assert.That(stalled, Is.True);
            Assert.That(slowest, Is.LessThan(t.stallSpeed));
            Assert.That(lowestPitch, Is.LessThan(0f), "the nose should fall through the horizon");
            Assert.That(s.Speed, Is.GreaterThan(t.stallSpeed), "and speed come back");
        }

        [Test]
        public void DivingBuildsSpeedPastTheThrottleSetting()
        {
            var t = new ArenaFlightTuning { gravity = 15f };
            ArenaFlightState s = Start(t, 95f);
            s.Attitude = Quaternion.Euler(45f, 0f, 0f);
            s = Fly(t, Vector2.zero, 0f, 0f, 1.5f, s);
            Assert.That(s.Speed, Is.GreaterThan(t.cruiseSpeed + 10f));
        }

        [Test]
        public void LevelFlightHoldsTheThrottleSpeed()
        {
            var t = new ArenaFlightTuning { gravity = 15f };
            ArenaFlightState s = Fly(t, Vector2.zero, 0f, 0f, 3f, Start(t, 50f));
            Assert.That(s.Speed, Is.EqualTo(t.cruiseSpeed).Within(0.01f));
            Assert.That(s.Stalled, Is.False);
        }

        [Test]
        public void WithoutEnergyClimbsKeepSpeedAndNeverStall()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Start(t, 10f);
            s.Attitude = Quaternion.Euler(-80f, 0f, 0f);
            s = Fly(t, Vector2.zero, 0f, 0f, 2f, s);
            Assert.That(s.Speed, Is.EqualTo(t.cruiseSpeed).Within(0.01f));
            Assert.That(s.Stalled, Is.False);
        }

        [Test]
        public void CruiseNotchSnapsTheMiddleAndKeepsTheEnds()
        {
            Assert.That(ArenaFlightModel.SnapCruise(0.05f, 0.08f), Is.EqualTo(0f));
            Assert.That(ArenaFlightModel.SnapCruise(-0.07f, 0.08f), Is.EqualTo(0f));
            Assert.That(ArenaFlightModel.SnapCruise(1f, 0.08f), Is.EqualTo(1f));
            Assert.That(ArenaFlightModel.SnapCruise(-1f, 0.08f), Is.EqualTo(-1f));
            Assert.That(ArenaFlightModel.SnapCruise(0.54f, 0.08f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void ShapeKeepsSignAndEnds()
        {
            Assert.That(ArenaFlightModel.Shape(1f, 2f), Is.EqualTo(1f));
            Assert.That(ArenaFlightModel.Shape(-1f, 2f), Is.EqualTo(-1f));
            Assert.That(ArenaFlightModel.Shape(0.5f, 2f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(ArenaFlightModel.Shape(-0.5f, 2f), Is.EqualTo(-0.25f).Within(1e-5f));
        }

        [Test]
        public void ShotThroughPointPassesThroughIt()
        {
            var muzzle = new Vector3(1f, 0f, 0f);
            var point = new Vector3(10f, 5f, 60f);
            var ship = new Vector3(8f, 0f, 30f);
            Vector3 v = WeaponRules.ShotVelocityThrough(muzzle, point, 70f, ship);
            Vector3 toPoint = (point - muzzle).normalized;
            Assert.That(Vector3.Angle(v, toPoint), Is.LessThan(0.01f));
            Assert.That((v - ship).magnitude, Is.EqualTo(70f).Within(0.01f), "muzzle speed is unchanged");
        }

        [Test]
        public void ShotVelocityInheritsTheWholeShipVelocity()
        {
            var ship = new Vector3(10f, 0f, 20f);
            Vector3 v = WeaponRules.ShotVelocity(Vector3.zero, Vector3.forward * 25f, 70f, ship);
            Assert.That(v, Is.EqualTo(Vector3.forward * 70f + ship));
        }

        [Test]
        public void AssistPicksTheTargetNearestTheFiringLine()
        {
            var candidates = new[]
            {
                new Vector3(0f, 0f, -50f),  // behind
                new Vector3(6f, 0f, 50f),   // ~6.8 degrees off
                new Vector3(2f, 0f, 50f),   // ~2.3 degrees off
                new Vector3(1f, 0f, 500f),  // out of range
            };
            Assert.That(WeaponRules.PickAssistTarget(Vector3.zero, Vector3.forward, candidates, 4, 8f, 120f),
                Is.EqualTo(2));
            Assert.That(WeaponRules.PickAssistTarget(Vector3.zero, Vector3.forward, candidates, 4, 2f, 120f),
                Is.EqualTo(-1));
        }

        [Test]
        public void TwistUsesItsOwnCentreAndDeadzone()
        {
            var c = new ControllerCalibration { twistCenter = 500, twistDeadzone = 0.12f };
            Assert.That(c.NormalizeTwist(500), Is.EqualTo(0f));
            Assert.That(c.NormalizeTwist(540), Is.EqualTo(0f), "inside the deadzone");
            Assert.That(c.NormalizeTwist(1023), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(c.NormalizeTwist(0), Is.EqualTo(-1f).Within(1e-5f));
            c.invertTwist = true;
            Assert.That(c.NormalizeTwist(1023), Is.EqualTo(-1f).Within(1e-5f));
        }
    }
}

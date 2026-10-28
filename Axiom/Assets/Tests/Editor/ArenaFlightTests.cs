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

        private static ArenaFlightState Start(ArenaFlightTuning t) =>
            new ArenaFlightState { Position = new Vector3(0f, 30f, 0f), Speed = t.cruiseSpeed };

        private static ArenaFlightState Fly(ArenaFlightTuning t, Vector2 stick, float twist, float throttle,
            float seconds, ArenaFlightState? from = null)
        {
            ArenaFlightState s = from ?? Start(t);
            for (float time = 0f; time < seconds; time += Dt)
                ArenaFlightModel.Step(ref s, stick, twist, throttle, t, Arena, Dt);
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
        public void RightStickBanksAndTurnsRight()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Fly(t, Vector2.right, 0f, 0f, 0.5f);
            Assert.That(s.Bank, Is.GreaterThan(t.maxBank * 0.9f));
            Assert.That(s.Yaw, Is.GreaterThan(20f).And.LessThan(90f));
        }

        [Test]
        public void TurnIsFastestOnTheDetent()
        {
            var t = new ArenaFlightTuning();
            float atCruise = Fly(t, Vector2.right, 0f, 0f, 1f).Yaw;
            float atMin = Fly(t, Vector2.right, 0f, -1f, 1f).Yaw;
            float atMax = Fly(t, Vector2.right, 0f, 1f, 1f).Yaw;
            Assert.That(atCruise, Is.GreaterThan(atMin));
            Assert.That(atCruise, Is.GreaterThan(atMax));
        }

        [Test]
        public void BrakeEndGivesTheSmallestCircle()
        {
            var t = new ArenaFlightTuning();
            float speedMin = t.cruiseSpeed * t.throttleMultipliers.x;
            float speedMax = t.cruiseSpeed * t.throttleMultipliers.y;
            float rMin = ArenaFlightModel.TurnRadius(speedMin, t.turnRate * t.turnAtThrottleEnds.x);
            float rCruise = ArenaFlightModel.TurnRadius(t.cruiseSpeed, t.turnRate);
            float rMax = ArenaFlightModel.TurnRadius(speedMax, t.turnRate * t.turnAtThrottleEnds.y);
            Assert.That(rMin, Is.LessThan(rCruise));
            Assert.That(rCruise, Is.LessThan(rMax));
        }

        [Test]
        public void TwistYawsWithoutBanking()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Fly(t, Vector2.zero, 1f, 0f, 1f);
            Assert.That(s.Bank, Is.EqualTo(0f).Within(0.001f));
            Assert.That(s.Yaw, Is.EqualTo(t.twistYawRate).Within(1f));
        }

        [Test]
        public void PitchIsClampedAndLevelsWhenReleased()
        {
            var t = new ArenaFlightTuning { gravity = 0f };
            ArenaFlightState climbing = Fly(t, Vector2.up, 0f, 0f, 3f, new ArenaFlightState
            {
                Position = new Vector3(0f, 10f, 0f), Speed = t.cruiseSpeed,
            });
            Assert.That(climbing.Pitch, Is.LessThanOrEqualTo(t.maxPitch));
            ArenaFlightState released = Fly(t, Vector2.zero, 0f, 0f, 5f, climbing);
            Assert.That(Mathf.Abs(released.Pitch), Is.LessThan(3f));
        }

        [Test]
        public void NeverLeavesTheArena()
        {
            var t = new ArenaFlightTuning();
            // Full throttle, diving and climbing, with the twist fighting the return turn.
            foreach (Vector2 stick in new[] { Vector2.zero, Vector2.up, Vector2.down, new Vector2(0.3f, 1f) })
            {
                ArenaFlightState s = Start(t);
                for (int i = 0; i < 60 * 40; i++)
                {
                    ArenaFlightModel.Step(ref s, stick, 1f, 1f, t, Arena, Dt);
                    var flat = new Vector2(s.Position.x, s.Position.z);
                    Assert.That(flat.magnitude, Is.LessThanOrEqualTo(Arena.Radius + 0.001f));
                    Assert.That(s.Position.y, Is.InRange(Arena.MinAltitude - 0.001f, Arena.Ceiling + 0.001f));
                }
            }
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
                ArenaFlightModel.Step(ref s, Vector2.zero, 0f, 0f, t, Arena, Dt);
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
            ArenaFlightModel.Step(ref s, Vector2.one, 1f, 1f, t, Arena, 0f);
            Assert.That(s.Position, Is.EqualTo(before.Position));
            Assert.That(s.Yaw, Is.EqualTo(before.Yaw));
        }

        private static ArenaFlightState FullStart(ArenaFlightTuning t, float altitude)
        {
            var s = new ArenaFlightState { Position = new Vector3(0f, altitude, 0f), Speed = t.cruiseSpeed };
            s.SetFullRotation(true);
            return s;
        }

        private static ArenaFlightState FlyFull(ArenaFlightTuning t, ArenaFlightState s, Vector2 stick, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) ArenaFlightModel.StepFull(ref s, stick, 0f, 0f, t, Arena, Dt);
            return s;
        }

        [Test]
        public void FullRotationHeldBackFliesALoop()
        {
            var t = new ArenaFlightTuning { gravity = 0f }; // geometry only: no speed lost on the way up
            ArenaFlightState s = FullStart(t, 50f);
            ArenaFlightState half = FlyFull(t, s, Vector2.up, 180f / t.loopRate);
            Assert.That(Vector3.Dot(half.Rotation * Vector3.up, Vector3.up), Is.LessThan(-0.95f), "inverted at the top");
            Assert.That(half.Position.y, Is.GreaterThan(50f + 2f * ArenaFlightModel.TurnRadius(t.cruiseSpeed, t.loopRate) - 3f));
            ArenaFlightState full = FlyFull(t, half, Vector2.up, 180f / t.loopRate);
            Assert.That(Vector3.Angle(full.Forward, Vector3.forward), Is.LessThan(2f), "back to the starting heading");
            Assert.That(full.Position.y, Is.EqualTo(50f).Within(1.5f));
        }

        [Test]
        public void FullRotationHeldRightFliesABarrelRoll()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = FlyFull(t, FullStart(t, 50f), Vector2.right, 360f / t.rollRate);
            Assert.That(Quaternion.Angle(s.Rotation, Quaternion.identity), Is.LessThan(2f));
            Assert.That(s.Position.y, Is.EqualTo(50f).Within(0.01f), "a roll alone doesn't change height");
        }

        [Test]
        public void FullRotationNeverLeavesTheArena()
        {
            var t = new ArenaFlightTuning();
            foreach (Vector2 stick in new[] { Vector2.zero, Vector2.up, Vector2.down, new Vector2(1f, 0.4f), new Vector2(-0.2f, -1f) })
            {
                ArenaFlightState s = FullStart(t, 30f);
                for (int i = 0; i < 60 * 40; i++)
                {
                    ArenaFlightModel.StepFull(ref s, stick, 1f, 1f, t, Arena, Dt);
                    var flat = new Vector2(s.Position.x, s.Position.z);
                    Assert.That(flat.magnitude, Is.LessThanOrEqualTo(Arena.Radius + 0.001f));
                    Assert.That(s.Position.y, Is.InRange(Arena.MinAltitude - 0.001f, Arena.Ceiling + 0.001f));
                }
            }
        }

        [Test]
        public void SwitchingModesKeepsTheHeading()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = Fly(t, Vector2.right, 0f, 0f, 0.7f);
            Vector3 before = s.Forward;
            s.SetFullRotation(true);
            Assert.That(Vector3.Angle(s.Forward, before), Is.LessThan(0.01f));
            s = FlyFull(t, s, new Vector2(0.3f, 0.5f), 0.5f);
            Vector3 full = s.Forward;
            s.SetFullRotation(false);
            Assert.That(Vector3.Angle(s.Forward, full), Is.LessThan(0.5f));
        }

        [Test]
        public void ClimbingBleedsSpeedIntoAStallAndTheNoseFalls()
        {
            var t = new ArenaFlightTuning { gravity = 15f };
            ArenaFlightState s = FullStart(t, 10f);
            s.Attitude = Quaternion.Euler(-80f, 0f, 0f);
            bool stalled = false;
            float slowest = s.Speed, lowestPitch = 90f;
            for (int i = 0; i < 60 * 6; i++)
            {
                ArenaFlightModel.StepFull(ref s, Vector2.zero, 0f, 0f, t, Arena, Dt);
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
            ArenaFlightState s = FullStart(t, 95f);
            s.Attitude = Quaternion.Euler(45f, 0f, 0f);
            s = FlyFull(t, s, Vector2.zero, 1.5f);
            Assert.That(s.Speed, Is.GreaterThan(t.cruiseSpeed + 10f));
        }

        [Test]
        public void LevelFlightHoldsTheThrottleSpeed()
        {
            var t = new ArenaFlightTuning { gravity = 15f };
            ArenaFlightState s = FlyFull(t, FullStart(t, 50f), Vector2.zero, 3f);
            Assert.That(s.Speed, Is.EqualTo(t.cruiseSpeed).Within(0.01f));
            Assert.That(s.Stalled, Is.False);
        }

        [Test]
        public void WithoutEnergyClimbsKeepSpeedAndNeverStall()
        {
            var t = new ArenaFlightTuning();
            ArenaFlightState s = FullStart(t, 10f);
            s.Attitude = Quaternion.Euler(-80f, 0f, 0f);
            s = FlyFull(t, s, Vector2.zero, 2f);
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

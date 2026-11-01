using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class RailSealTests
    {
        private const float CloseSeconds = 0.4f; // RailSeal's default iris close
        private const float HullReach = 1.3f;    // The hull's half length: how far any part can lead the centre

        /// <summary>
        /// The seal arms as the hull clears the passage's end plane. Every reachable exit (heading within the rail's
        /// 45° limit, any speed up to boost, any throttle, any full stick or twist, both handling modes) must take
        /// longer to bring any part of the hull back to that plane than the iris takes to close.
        /// </summary>
        [Test]
        public void NoShipCanTurnBackBeforeTheSealCloses()
        {
            var tuning = new ArenaFlightTuning();
            var limits = new ArenaLimits(Vector3.zero, 1e9f, -1e9f, 1e9f);
            float earliest = float.MaxValue;
            float[] angles = { -45f, 0f, 45f };
            Vector2[] sticks =
            {
                new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
                new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f),
            };
            foreach (bool full in new[] { true, false })
            foreach (float speed in new[] { 18f, 30f, 45f, 70f })
            foreach (float throttle in new[] { -1f, 0f, 1f })
            foreach (float yaw in angles)
            foreach (float pitch in angles)
            foreach (Vector2 stick in sticks)
            foreach (float twist in new[] { -1f, 0f, 1f })
            {
                var s = new ArenaFlightState
                {
                    Position = new Vector3(0f, 16f, HullReach), // the hull's rear has just cleared z = 0
                    Yaw = yaw, Pitch = pitch, Speed = speed,
                };
                s.SetFullRotation(full);
                if (full) s.Attitude = Quaternion.Euler(-pitch, yaw, 0f);
                const float dt = 1f / 120f;
                for (float t = 0f; t < 4f; t += dt)
                {
                    if (full) ArenaFlightModel.StepFull(ref s, stick, twist, throttle, tuning, limits, dt);
                    else ArenaFlightModel.Step(ref s, stick, twist, throttle, tuning, limits, dt);
                    if (s.Position.z - HullReach <= 0f) { earliest = Mathf.Min(earliest, t + dt); break; }
                }
            }
            Assert.That(earliest, Is.GreaterThan(CloseSeconds + 0.1f),
                $"a ship got back to the end plane after {earliest:0.00} s, before the {CloseSeconds} s seal");
            TestContext.WriteLine($"Earliest possible return to the plane: {earliest:0.00} s");
        }
    }
}

using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class FlightMovementTests
    {
        [TestCase(-2f, 3.6f)]
        [TestCase(-1f, 3.6f)]
        [TestCase(0f, 6f)]
        [TestCase(1f, 9f)]
        [TestCase(2f, 9f)]
        public void ThrottleMapsToBoundedForwardSpeed(float throttle, float expected)
        {
            Assert.That(FlightMovement.TargetSpeed(throttle, 6f, new Vector2(0.6f, 1.5f)),
                Is.EqualTo(expected).Within(0.00001f));
        }

        [TestCase(3.6f)]
        [TestCase(9f)]
        public void AccelerationTravelIsIndependentOfFrameRate(float target)
        {
            float expectedSpeed = 6f;
            float expectedDistance = FlightMovement.Accelerate(ref expectedSpeed, target, 6f, 2f) * 2f;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float speed = 6f;
                float distance = 0f;
                for (int frame = 0; frame < fps * 2; frame++)
                    distance += FlightMovement.Accelerate(ref speed, target, 6f, 1f / fps) / fps;
                Assert.That(speed, Is.EqualTo(target).Within(0.00001f));
                Assert.That(distance, Is.EqualTo(expectedDistance).Within(0.0001f));
            }
        }

        [Test]
        public void AccelerationIsGradualAndDoesNotOvershoot()
        {
            float speed = 6f;
            Assert.That(FlightMovement.Accelerate(ref speed, 9f, 6f, 0.1f), Is.EqualTo(6.3f).Within(0.00001f));
            Assert.That(speed, Is.EqualTo(6.6f).Within(0.00001f));
            FlightMovement.Accelerate(ref speed, 9f, 6f, 10f);
            Assert.That(speed, Is.EqualTo(9f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void PausedAccelerationDoesNotChangeSpeed(float deltaTime)
        {
            float speed = 6f;
            FlightMovement.Accelerate(ref speed, 9f, 6f, deltaTime);
            Assert.That(speed, Is.EqualTo(6f));
        }
    }
}

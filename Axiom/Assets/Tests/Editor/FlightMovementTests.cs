using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class FlightMovementTests
    {
        private static readonly Rect Bounds = Rect.MinMaxRect(-6f, -2f, 6f, 4f);

        [Test]
        public void NoInputStillMovesForward()
        {
            Assert.That(Step(new Vector3(1f, 2f, 3f), Vector2.zero, 0.5f),
                Is.EqualTo(new Vector3(1f, 2f, 6f)));
        }

        [Test]
        public void DiagonalInputHasSameSpeedAsCardinalInput()
        {
            Vector3 diagonal = Step(Vector3.zero, Vector2.one, 0.1f);
            Vector3 cardinal = Step(Vector3.zero, Vector2.right, 0.1f);
            Assert.That(new Vector2(diagonal.x, diagonal.y).magnitude,
                Is.EqualTo(cardinal.x).Within(0.00001f));
            Assert.That(diagonal.z, Is.EqualTo(cardinal.z));
        }

        [Test]
        public void PartialAnalogInputPreservesMagnitude()
        {
            Assert.That(Step(Vector3.zero, Vector2.right * 0.25f, 1f).x, Is.EqualTo(1.25f));
        }

        [TestCase(1f, 0f, 6f, 0f)]
        [TestCase(-1f, 0f, -6f, 0f)]
        [TestCase(0f, 1f, 0f, 4f)]
        [TestCase(0f, -1f, 0f, -2f)]
        [TestCase(1f, 1f, 6f, 4f)]
        [TestCase(-1f, -1f, -6f, -2f)]
        public void LongFrameCannotCrossBounds(float x, float y, float expectedX, float expectedY)
        {
            Vector3 result = Step(Vector3.zero, new Vector2(x, y), 10f);
            Assert.That(result, Is.EqualTo(new Vector3(expectedX, expectedY, 60f)));
        }

        [Test]
        public void ReversingAtBoundaryRespondsImmediately()
        {
            Vector3 atEdge = Step(Vector3.zero, Vector2.right, 10f);
            Assert.That(Step(atEdge, Vector2.left, 0.1f).x, Is.EqualTo(5.5f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveTimeDoesNotMove(float deltaTime)
        {
            Vector3 position = new Vector3(1f, 2f, 3f);
            Assert.That(Step(position, Vector2.one, deltaTime), Is.EqualTo(position));
        }

        [Test]
        public void TravelIsIndependentOfFrameRate()
        {
            Vector3 position = Vector3.zero;
            for (int i = 0; i < 60; i++)
                position = Step(position, new Vector2(0.5f, 0.25f), 1f / 60f);
            Vector3 expected = Step(Vector3.zero, new Vector2(0.5f, 0.25f), 1f);
            Assert.That(Vector3.Distance(position, expected), Is.LessThan(0.0001f));
        }

        [Test]
        public void BoundsCanBeOffsetFromWorldOrigin()
        {
            Vector3 result = FlightMovement.Step(new Vector3(20f, 10f, 30f), Vector2.one,
                10f, 6f, 5f, Rect.MinMaxRect(14f, 8f, 26f, 14f));
            Assert.That(result, Is.EqualTo(new Vector3(26f, 14f, 90f)));
        }

        private static Vector3 Step(Vector3 position, Vector2 input, float deltaTime) =>
            FlightMovement.Step(position, input, deltaTime, 6f, 5f, Bounds);
    }
}

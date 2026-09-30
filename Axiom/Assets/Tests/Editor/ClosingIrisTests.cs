using GeometricFlight.Combat;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ClosingIrisTests
    {
        private static readonly Vector2 Hull = new Vector2(1.2f, 0.3f);

        [Test]
        public void CenteredHullFitsUntilApertureNearlyShut()
        {
            // Tightest slab is the 30° side: 1.2·cos30 + 0.3·sin30 ≈ 1.189.
            Assert.That(ClosingIris.RectFitsAperture(Vector2.zero, Hull, 1.2f, 0f), Is.True);
            Assert.That(ClosingIris.RectFitsAperture(Vector2.zero, Hull, 1.18f, 0f), Is.False);
            Assert.That(ClosingIris.RectFitsAperture(Vector2.zero, Hull, 0f, 0f), Is.False);
        }

        [Test]
        public void OffCenterHullNeedsMoreRoom()
        {
            Assert.That(ClosingIris.RectFitsAperture(new Vector2(2f, 0f), Hull, 2f, 0f), Is.False);
            Assert.That(ClosingIris.RectFitsAperture(new Vector2(2f, 0f), Hull, 3f, 0f), Is.True);
        }

        [Test]
        public void RotationMatters()
        {
            // Unrotated, a flat side caps +Y at the apothem; twisted 30°, a vertex points up and
            // reaches apothem / cos 30°, so a tall, thin rectangle only fits once twisted.
            var tall = new Vector2(0.1f, 1.75f);
            Assert.That(ClosingIris.RectFitsAperture(Vector2.zero, tall, 1.65f, 0f), Is.False);
            Assert.That(ClosingIris.RectFitsAperture(Vector2.zero, tall, 1.65f, 30f), Is.True);
        }

        [Test]
        public void FullyOpenIrisContainsWholeFlightArea()
        {
            // Flight area x ±6, y −2..4 around the iris center (0, 1), plus the hull.
            foreach (var corner in new[] { new Vector2(6f, 3f), new Vector2(-6f, -3f), new Vector2(6f, -3f) })
                Assert.That(ClosingIris.RectFitsAperture(corner, Hull, 8.5f, 0f), Is.True);
        }
    }
}

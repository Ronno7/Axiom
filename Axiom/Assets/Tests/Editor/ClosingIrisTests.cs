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

        [Test]
        public void UnrotatedHullProjectionPreservesLegacyRectangleFit()
        {
            var extents = new Vector3(Hull.x, Hull.y, 2f);
            foreach (float twist in new[] { 0f, 15f, 50f })
                foreach (float aperture in new[] { 0f, 1.18f, 1.2f, 3f })
                    foreach (Vector2 offset in new[] { Vector2.zero, new Vector2(2f, 0.4f) })
                    {
                        var matrix = Matrix4x4.Translate(new Vector3(offset.x, offset.y, 0f));
                        Assert.That(ClosingIris.BoxFitsAperture(matrix, Vector3.zero, extents, aperture, twist),
                            Is.EqualTo(ClosingIris.RectFitsAperture(offset, Hull, aperture, twist)));
                    }
        }

        [Test]
        public void RollChangesWhetherAWideHullFits()
        {
            var wide = new Vector3(1.8f, 0.1f, 0.1f);
            Assert.That(ClosingIris.BoxFitsAperture(Matrix4x4.identity, Vector3.zero, wide, 1.7f, 0f), Is.True);
            var rolled = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
            Assert.That(ClosingIris.BoxFitsAperture(rolled, Vector3.zero, wide, 1.7f, 0f), Is.False,
                "The long wing now reaches above the flat top of the aperture.");
        }

        [Test]
        public void PitchedHullDepthContributesToItsSilhouette()
        {
            var longHull = new Vector3(0.1f, 0.1f, 2f);
            Assert.That(ClosingIris.BoxFitsAperture(Matrix4x4.identity, Vector3.zero, longHull, 1.7f, 0f), Is.True);
            var pitched = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f));
            Assert.That(ClosingIris.BoxFitsAperture(pitched, Vector3.zero, longHull, 1.7f, 0f), Is.False,
                "Ignoring local Z extents would let a nose-up ship clip through the blades.");
        }

        [Test]
        public void ColliderCenterAndNonuniformScaleAffectClearance()
        {
            Vector3 halfExtents = Vector3.one * 0.4f;
            Assert.That(ClosingIris.BoxFitsAperture(Matrix4x4.identity, Vector3.right * 0.5f,
                halfExtents, 1.2f, 0f), Is.True);
            Assert.That(ClosingIris.BoxFitsAperture(Matrix4x4.Scale(new Vector3(2f, 1f, 1f)),
                Vector3.right * 0.5f, halfExtents, 1.2f, 0f), Is.False);
        }

        [Test]
        public void MovingAndRotatingTheWholeIrisFrameDoesNotChangeFit()
        {
            var irisToWorld = Matrix4x4.TRS(new Vector3(80f, 25f, 200f),
                Quaternion.Euler(15f, 40f, 25f), Vector3.one * 3f);
            var relativeHull = Matrix4x4.TRS(new Vector3(1f, -0.3f, 0f),
                Quaternion.Euler(30f, 0f, 70f), new Vector3(1f, 2f, 1f));
            Matrix4x4 worldHull = irisToWorld * relativeHull;
            foreach (float aperture in new[] { 0.5f, 1.5f, 4f })
                Assert.That(ClosingIris.BoxFitsAperture(irisToWorld.inverse * worldHull,
                    Vector3.zero, Vector3.one * 0.5f, aperture, 20f),
                    Is.EqualTo(ClosingIris.BoxFitsAperture(relativeHull,
                        Vector3.zero, Vector3.one * 0.5f, aperture, 20f)));
        }

        [Test]
        public void CrossingSamplesThePlaneInsteadOfTheOvershotFrameEnd()
        {
            Vector3 before = new Vector3(-1f, 0f, -1f);
            Vector3 after = new Vector3(5f, 0f, 3f);
            Assert.That(ClosingIris.TryForwardCrossing(before.z, after.z, out float fraction), Is.True);
            Assert.That(fraction, Is.EqualTo(0.25f).Within(0.00001f));
            Vector3 atPlane = Vector3.Lerp(before, after, fraction);
            Assert.That(atPlane.z, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(ClosingIris.BoxFitsAperture(Matrix4x4.Translate(atPlane),
                Vector3.zero, Vector3.one * 0.3f, 1f, 0f), Is.True);
            Assert.That(ClosingIris.BoxFitsAperture(Matrix4x4.Translate(after),
                Vector3.zero, Vector3.one * 0.3f, 1f, 0f), Is.False);
        }

        [TestCase(-3f, -1f)]
        [TestCase(1f, -1f)]
        [TestCase(0f, 1f)]
        [TestCase(1f, 2f)]
        public void OnlyAFreshForwardCrossingTriggers(float before, float after)
        {
            Assert.That(ClosingIris.TryForwardCrossing(before, after, out _), Is.False);
        }
    }
}

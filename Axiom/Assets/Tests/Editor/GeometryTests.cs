using GeometricFlight.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class GeometryTests
    {
        [TestCase(0.1f, 0f)]
        [TestCase(1.3f, 0.5f)]
        [TestCase(5.9f, 0.9f)]
        public void KeplerSolutionSatisfiesKeplersEquation(float meanAnomaly, float e)
        {
            float E = KeplerOrbit.SolveKepler(meanAnomaly, e);
            Assert.That(E - e * Mathf.Sin(E), Is.EqualTo(meanAnomaly).Within(1e-4f));
        }

        [Test]
        public void OrbitIsFastestAtPeriapsis()
        {
            var orbit = new KeplerOrbit(3f, 0.5f, 6f, 0f, Quaternion.identity);
            const float dt = 0.01f;
            // Phase 0 starts at periapsis (distance a(1 − e)); half a period later is apoapsis.
            Assert.That(orbit.PositionAt(0f).magnitude, Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(orbit.PositionAt(3f).magnitude, Is.EqualTo(4.5f).Within(1e-3f));
            float periapsisSpeed = Vector3.Distance(orbit.PositionAt(0f), orbit.PositionAt(dt)) / dt;
            float apoapsisSpeed = Vector3.Distance(orbit.PositionAt(3f), orbit.PositionAt(3f + dt)) / dt;
            // Angular momentum conservation: v_p / v_a = (1 + e) / (1 − e) = 3.
            Assert.That(periapsisSpeed / apoapsisSpeed, Is.EqualTo(3f).Within(0.05f));
        }

        [Test]
        public void TumbleConservesEnergyAndMomentumAndFlips()
        {
            var body = new TumblingBody(new Vector3(1f, 2f, 3f), new Vector3(0.02f, 0.8f, 0.02f), Quaternion.identity);
            float energy = body.KineticEnergy;
            Vector3 momentum = body.WorldAngularMomentum;
            bool flipped = false;
            for (int i = 0; i < 60 * 40; i++)
            {
                body.Step(1f / 60f);
                flipped |= body.AngularVelocity.y < 0f;
            }
            Assert.That(body.KineticEnergy, Is.EqualTo(energy).Within(energy * 0.005f));
            Assert.That(Vector3.Angle(body.WorldAngularMomentum, momentum), Is.LessThan(2f));
            Assert.That(flipped, Is.True, "intermediate-axis spin should flip (Dzhanibekov effect)");
        }

        [Test]
        public void PlatonicCagesHaveCorrectStructure()
        {
            Assert.That(GeometryMeshes.IcosahedronVertices().Length, Is.EqualTo(12));
            Assert.That(GeometryMeshes.ShortestEdges(GeometryMeshes.IcosahedronVertices()).Count, Is.EqualTo(30));
            Assert.That(GeometryMeshes.DodecahedronVertices().Length, Is.EqualTo(20));
            Assert.That(GeometryMeshes.ShortestEdges(GeometryMeshes.DodecahedronVertices()).Count, Is.EqualTo(30));
        }

        [Test]
        public void ClosedTubeSeamMeets()
        {
            Mesh tube = GeometryMeshes.ClosedTube(t => GeometryMeshes.TorusKnot(t, 2, 3), 180, 0.1f, 6, "Test");
            try
            {
                // Every vertex lies at tube radius from the curve; a mis-closed seam would stretch
                // the last quads far beyond that.
                Vector3[] v = tube.vertices;
                int[] tri = tube.triangles;
                float longest = 0f;
                for (int i = 0; i < tri.Length; i += 3)
                    longest = Mathf.Max(longest, Vector3.Distance(v[tri[i]], v[tri[i + 1]]),
                        Vector3.Distance(v[tri[i + 1]], v[tri[i + 2]]));
                Assert.That(longest, Is.LessThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(tube);
            }
        }
    }
}

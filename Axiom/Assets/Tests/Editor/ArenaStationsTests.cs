using GeometricFlight.Core;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ArenaStationsTests
    {
        private const float Preferred = 520f, Nearest = 500f;

        private static Vector3[] Ring()
        {
            var points = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Sin(angle) * 520f, 120f, 6580f + Mathf.Cos(angle) * 520f);
            }
            return points;
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [Test]
        public void LeavingTheLastRing_TheStationIsFarAndNearlyAhead()
        {
            Vector3[] ring = Ring();
            Vector3 ship = new Vector3(230f, 86f, 6405f), nose = new Vector3(-0.68f, 0.14f, 0.72f);
            Vector3 pick = ring[ArenaStations.Pick(ship, nose, ring, Preferred, Nearest)];
            Assert.Greater(Flat(pick, ship), Nearest);
            Assert.Less(Vector2.Angle(new Vector2(nose.x, nose.z), new Vector2(pick.x - ship.x, pick.z - ship.z)), 30f);
        }

        [Test]
        public void AnywhereInTheArena_FlyingAnyWay_ThereIsRoomToAct()
        {
            Vector3[] ring = Ring();
            for (int a = 0; a < 16; a++)
                for (float r = 0f; r <= 720f; r += 120f)
                {
                    float angle = a * 22.5f * Mathf.Deg2Rad;
                    var ship = new Vector3(Mathf.Sin(angle) * r, 80f, 6580f + Mathf.Cos(angle) * r);
                    for (int h = 0; h < 8; h++)
                    {
                        float heading = h * 45f * Mathf.Deg2Rad;
                        var nose = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
                        Vector3 pick = ring[ArenaStations.Pick(ship, nose, ring, Preferred, Nearest)];
                        Assert.GreaterOrEqual(Flat(pick, ship), Nearest, $"ship {ship}, heading {h * 45}");
                    }
                }
        }

        [Test]
        public void ABlockedStation_GivesWayToAClearOneNearby()
        {
            Vector3[] ring = { new Vector3(0f, 0f, 500f), new Vector3(150f, 0f, 500f) };
            int pick = ArenaStations.Pick(Vector3.zero, Vector3.forward, ring, Preferred, Nearest, i => i == 0);
            Assert.AreEqual(1, pick);
        }

        [Test]
        public void TheStationJustUsed_GivesWayToAnother()
        {
            Vector3[] ring = { new Vector3(0f, 0f, 520f), new Vector3(300f, 0f, 520f) };
            Assert.AreEqual(0, ArenaStations.Pick(Vector3.zero, Vector3.forward, ring, Preferred, Nearest));
            Assert.AreEqual(1, ArenaStations.Pick(Vector3.zero, Vector3.forward, ring, Preferred, Nearest, avoid: 0));
        }

        [Test]
        public void WithEveryStationTooClose_TheFarthestIsUsed()
        {
            Vector3[] ring = { new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 300f) };
            Assert.AreEqual(1, ArenaStations.Pick(Vector3.zero, Vector3.forward, ring, Preferred, Nearest));
        }

        [Test]
        public void NoStations_ReturnsNone() =>
            Assert.AreEqual(-1, ArenaStations.Pick(Vector3.zero, Vector3.forward, new Vector3[0], Preferred, Nearest));
    }
}

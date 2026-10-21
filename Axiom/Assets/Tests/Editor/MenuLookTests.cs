using GeometricFlight.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class MenuLookTests
    {
        // ---------- Glitch timeline ----------

        [Test]
        public void GlitchEventStartsAndEndsCalm()
        {
            int last = MenuGlitchTimeline.FrameCount - 1;
            foreach (int i in new[] { 0, last, -1, last + 1 })
            {
                GlitchFrame f = MenuGlitchTimeline.Frame(i);
                Assert.That(f.Tear, Is.Zero, $"frame {i}");
                Assert.That(f.EchoAmount, Is.Zero, $"frame {i}");
                Assert.That(f.RollY, Is.LessThan(0f), $"frame {i}");
                Assert.That(f.Drop || f.Scramble, Is.False, $"frame {i}");
            }
        }

        [Test]
        public void GlitchPeakIsTheStrongestTear()
        {
            float peak = MenuGlitchTimeline.Frame(MenuGlitchTimeline.PeakFrame).Tear;
            for (int i = 0; i < MenuGlitchTimeline.FrameCount; i++)
                Assert.That(MenuGlitchTimeline.Frame(i).Tear, Is.LessThanOrEqualTo(peak));
            Assert.That(peak, Is.GreaterThan(0f));
        }

        [Test]
        public void RollBarOnlyMovesDownTheScreen()
        {
            float previous = -1f;
            for (int i = 0; i < MenuGlitchTimeline.FrameCount; i++)
            {
                float y = MenuGlitchTimeline.Frame(i).RollY;
                if (y < 0f) continue;
                Assert.That(y, Is.InRange(0f, 1f));
                Assert.That(y, Is.GreaterThan(previous));
                previous = y;
            }
        }

        [Test]
        public void ScrambleKeepsLengthAndSpacesAndIsRepeatable()
        {
            const string text = "SYS ONLINE";
            string a = MenuGlitchTimeline.Scramble(text, 5);
            Assert.That(a.Length, Is.EqualTo(text.Length));
            Assert.That(a[3], Is.EqualTo(' '));
            Assert.That(a, Is.Not.EqualTo(text));
            Assert.That(MenuGlitchTimeline.Scramble(text, 5), Is.EqualTo(a));
            Assert.That(MenuGlitchTimeline.Scramble("", 5), Is.EqualTo(""));
        }

        [Test]
        public void IntervalStaysInRangeEitherWayRound()
        {
            var random = new System.Random(1);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(MenuGlitchTimeline.NextInterval(random, 8f, 12f), Is.InRange(8f, 12f));
                Assert.That(MenuGlitchTimeline.NextInterval(random, 12f, 8f), Is.InRange(8f, 12f));
            }
        }

        // ---------- Screen effects switch ----------

        [Test]
        public void ScreenPassRunsOnlyForItsCameraAndOnlyWhileAnEffectIsOn()
        {
            var camera = new GameObject("test camera").AddComponent<Camera>();
            var other = new GameObject("other camera").AddComponent<Camera>();
            try
            {
                ScreenEffects.Set(camera, Vector4.zero, Vector4.zero, Vector4.zero, 0f, Vector4.zero, Color.red);
                Assert.That(ScreenEffects.ShouldRender(camera), Is.False, "nothing on");

                ScreenEffects.Set(camera, Vector4.zero, Vector4.zero, Vector4.zero, 0f, new Vector4(0f, 0f, 0.02f, 0f), Color.red);
                Assert.That(ScreenEffects.ShouldRender(camera), Is.True, "static noise on");
                Assert.That(ScreenEffects.ShouldRender(other), Is.False, "other camera");

                ScreenEffects.Clear();
                Assert.That(ScreenEffects.ShouldRender(camera), Is.False, "cleared");
            }
            finally
            {
                ScreenEffects.Clear();
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(other.gameObject);
            }
        }

        // ---------- Stars and dust ----------

        [Test]
        public void BillboardsBuildFourVerticesPerDotInsideTheirRanges()
        {
            var eye = new Vector3(0f, 1.2f, 0f);
            var set = new GlowBillboards("test", new System.Random(7), 50, 10f, new Vector2(2f, 8f), new Vector2(20f, 40f),
                new Vector2(0.1f, 0.2f), new Vector2(0.1f, 1f), 3f, eye);
            try
            {
                Assert.That(set.Mesh.vertexCount, Is.EqualTo(200));
                Assert.That(set.Mesh.triangles.Length, Is.EqualTo(300));
                foreach (Vector3 c in set.Centers)
                {
                    Assert.That(c.x, Is.InRange(-10f, 10f));
                    Assert.That(c.y, Is.InRange(2f, 8f));
                    Assert.That(c.z, Is.InRange(20f, 40f));
                }
                foreach (Color c in set.Mesh.colors) Assert.That(c.a, Is.InRange(0.1f, 1f));
            }
            finally
            {
                Object.DestroyImmediate(set.Mesh);
            }
        }

        [Test]
        public void BillboardsFaceTheEye()
        {
            var eye = Vector3.zero;
            var set = new GlowBillboards("test", new System.Random(3), 10, 5f, new Vector2(-3f, 3f), new Vector2(10f, 20f),
                new Vector2(0.5f, 0.5f), Vector2.one, 1f, eye);
            try
            {
                Vector3[] v = set.Mesh.vertices;
                for (int i = 0; i < set.Centers.Length; i++)
                {
                    Vector3 normal = Vector3.Cross(v[i * 4 + 1] - v[i * 4], v[i * 4 + 3] - v[i * 4]).normalized;
                    Vector3 toEye = (eye - set.Centers[i]).normalized;
                    Assert.That(Mathf.Abs(Vector3.Dot(normal, toEye)), Is.GreaterThan(0.99f));
                }
            }
            finally
            {
                Object.DestroyImmediate(set.Mesh);
            }
        }
    }
}

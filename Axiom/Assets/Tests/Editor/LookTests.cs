using GeometricFlight.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Tests
{
    public sealed class LookTests
    {
        private GameObject host;
        private LookVolume look;
        private ArcadeProfile arcade;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("look test");
            look = new LookVolume(host.AddComponent<Volume>(), "test look");
            arcade = ScriptableObject.CreateInstance<ArcadeProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            look.Dispose();
            Object.DestroyImmediate(arcade);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void ArcadeColourAddsToTheGrade()
        {
            var grade = new GradeSettings { exposure = 0.2f, contrast = 12f };
            arcade.exposure = 0.3f;
            arcade.contrast = 95f;
            arcade.saturation = 40f;
            arcade.temperature = -20f;
            look.Apply(grade, arcade, null);

            look.Profile.TryGet(out ColorAdjustments colour);
            look.Profile.TryGet(out WhiteBalance white);
            Assert.That(colour.postExposure.value, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(colour.contrast.value, Is.EqualTo(100f), "clamped");
            Assert.That(colour.saturation.value, Is.EqualTo(40f));
            Assert.That(white.active, Is.True);
            Assert.That(white.temperature.value, Is.EqualTo(-20f));
        }

        [Test]
        public void WithoutArcadeTheGradeIsNeutralInColour()
        {
            arcade.saturation = 40f;
            look.Apply(new GradeSettings(), null, null);

            look.Profile.TryGet(out ColorAdjustments colour);
            look.Profile.TryGet(out WhiteBalance white);
            Assert.That(colour.saturation.value, Is.Zero);
            Assert.That(colour.colorFilter.value, Is.EqualTo(Color.white));
            Assert.That(white.active, Is.False);
        }

        [Test]
        public void ArcadeColourSwitchedOffLeavesTheGradeAlone()
        {
            var grade = new GradeSettings { contrast = 12f };
            arcade.colour = false;
            look.Apply(grade, arcade, null);

            look.Profile.TryGet(out ColorAdjustments colour);
            Assert.That(colour.contrast.value, Is.EqualTo(12f));
            Assert.That(colour.saturation.value, Is.Zero);
        }

        [Test]
        public void ArcadePointerFollowsTheCrtCurvature()
        {
            var screen = new Vector2(1920f, 1080f);
            var texture = new Vector2(1920f, 1080f);
            // Flat tube: the pointer lands where it is; the centre never moves.
            AssertNear(ArcadeInterface.ScreenToTexture(new Vector2(300f, 200f), screen, texture, 0f), new Vector2(300f, 200f));
            AssertNear(ArcadeInterface.ScreenToTexture(screen * 0.5f, screen, texture, 0.2f), screen * 0.5f);
            // Curved tube: off-centre points read further out, as the shader samples them.
            Vector2 bent = ArcadeInterface.ScreenToTexture(new Vector2(1600f, 900f), screen, texture, 0.035f);
            Assert.That(bent.x, Is.GreaterThan(1600f));
            Assert.That(bent.y, Is.GreaterThan(900f));
            // The black outside the tube is no hit.
            Assert.That(float.IsNaN(ArcadeInterface.ScreenToTexture(new Vector2(1919f, 1079f), screen, texture, 0.3f).x));
            // A smaller texture scales the position.
            AssertNear(ArcadeInterface.ScreenToTexture(new Vector2(960f, 0f), screen, screen * 0.5f, 0f), new Vector2(480f, 0f));
        }

        private static void AssertNear(Vector2 actual, Vector2 expected) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.01f), $"{actual} vs {expected}");

        [Test]
        public void DustWrapsIntoItsRangeWhereverTheCameraIs()
        {
            var depth = new Vector2(3f, 40f);
            foreach (float eye in new[] { -13f, 0f, 57.5f, 500f })
            foreach (float z in new[] { -200f, -1f, 0f, 20f, 39.9f, 300f })
            {
                float wrapped = LookAtmosphere.WrapDepth(z, eye, depth);
                Assert.That(wrapped - eye, Is.InRange(depth.x, depth.y), $"eye {eye}, z {z}");
            }
            Assert.That(LookAtmosphere.WrapDepth(10f, 0f, depth), Is.EqualTo(10f), "already inside");
        }
    }
}

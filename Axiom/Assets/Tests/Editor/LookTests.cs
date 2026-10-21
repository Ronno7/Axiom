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

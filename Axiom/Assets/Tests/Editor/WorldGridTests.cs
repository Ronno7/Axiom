using GeometricFlight.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>The floor grid holds true in world space (#59): the passage's markers sit on the ground's 12 m lines, and
    /// the menu moves the view over the grid instead of moving the grid.</summary>
    public sealed class WorldGridTests
    {
        [Test]
        public void PassageMarkersSitOnTheTwelveMetreLines()
        {
            // The scene's passage: ends at -270 and 5980, built at the world origin.
            var markers = EnclosedPassage.MarkerPositions(-270f, 5980f, 12f, 0f, 0f);
            Assert.That(markers[0], Is.EqualTo(-264f), "the first marker is the first line inside the mouth");
            Assert.That(markers[markers.Count - 1], Is.LessThan(5980f));
            foreach (float z in markers) Assert.That(Mathf.Repeat(z, 12f), Is.EqualTo(0f).Within(1e-3f), $"marker at {z}");
        }

        [Test]
        public void MarkersKeepTheirWorldPhaseWhereverThePassageStands()
        {
            var markers = EnclosedPassage.MarkerPositions(-150f, 900f, 12f, 0f, 37f);
            foreach (float z in markers)
                Assert.That(Mathf.Repeat(z + 37f, 12f), Is.EqualTo(0f).Within(1e-3f), $"world z {z + 37f}");
            Assert.That(markers[0], Is.GreaterThanOrEqualTo(-150f));
        }

        [Test]
        public void PassageRibsSitOnTheTwentyFourMetreLines()
        {
            // The scene's passage after step 2: mouth and exit on the grid too.
            var ribs = EnclosedPassage.RibPositions(-264f, 5976f, 24f);
            Assert.That(ribs[0], Is.EqualTo(-264f));
            Assert.That(ribs[ribs.Count - 1], Is.EqualTo(5976f));
            foreach (float z in ribs) Assert.That(Mathf.Repeat(z, 24f), Is.EqualTo(0f).Within(1e-3f), $"rib at {z}");
        }

        [Test]
        public void DustStaysDrawableWhereverTheCameraFlies()
        {
            var dust = new GlowBillboards("dust", new System.Random(1), 20, 14f, new Vector2(0.4f, 9f), new Vector2(3f, 30f),
                new Vector2(0.02f, 0.06f), new Vector2(0.15f, 0.55f), 1f, Vector3.zero);
            try
            {
                for (int i = 0; i < dust.Centers.Length; i++) dust.Centers[i] += Vector3.forward * 90f;
                dust.Write(Vector3.forward * 90f);
                Assert.That(dust.Mesh.bounds.min.z, Is.GreaterThan(80f), "the bounds follow the motes, so nothing culls them");
            }
            finally
            {
                Object.DestroyImmediate(dust.Mesh);
            }
        }

        [Test]
        public void TheMenuGlidesForwardAndStepsBackOneWholeRepeat()
        {
            Assert.That(MenuBackdrop.Glide(10f, 5f, 96f), Is.EqualTo(15f).Within(1e-4f), "real forward motion");
            float next = MenuBackdrop.Glide(94f, 5f, 96f);
            Assert.That(next, Is.EqualTo(3f).Within(1e-4f), "past one repeat it steps back");
            Assert.That(94f + 5f - next, Is.EqualTo(96f).Within(1e-4f), "by exactly the grid's repeat, so no line moves on screen");
            Assert.That(MenuBackdrop.Glide(10f, 5f, 0f), Is.EqualTo(15f), "no grid, no step");
        }

        [Test]
        public void TheGroundsRepeatIsItsMajorSpacing()
        {
            var go = new GameObject("ground");
            var material = new Material(Shader.Find("GeometricFlight/Grid Floor"));
            try
            {
                material.SetFloat("_MajorSpacing", 96f);
                var ground = go.AddComponent<InfiniteGround>();
                typeof(InfiniteGround).GetField("material", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).SetValue(ground, material);
                Assert.That(ground.RepeatLength, Is.EqualTo(96f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(material);
            }
        }
    }
}

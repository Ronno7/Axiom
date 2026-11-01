using System.Reflection;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>Material ownership at the reveal seam. The inactive rig avoids scene/input services.</summary>
    public sealed class SharedEntryPresentationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly int Marker = Shader.PropertyToID("_EntryTestMarker");
        private GameObject rig;
        private GameObject aircraft;
        private MeshRenderer renderer;
        private ShieldDisplay display;
        private SharedEntryPresentation presentation;
        private Material original, second, construction;

        [SetUp]
        public void SetUp()
        {
            rig = new GameObject("Entry presentation restoration test");
            rig.SetActive(false);
            aircraft = new GameObject("Aircraft");
            aircraft.transform.SetParent(rig.transform, false);
            renderer = aircraft.AddComponent<MeshRenderer>();
            var shield = rig.AddComponent<ShipShield>();
            display = rig.AddComponent<ShieldDisplay>();
            var ui = rig.AddComponent<LevelUi>();
            presentation = rig.AddComponent<SharedEntryPresentation>();
            Shader shader = Shader.Find("GeometricFlight/Grid Construction");
            Assert.That(shader, Is.Not.Null);
            original = new Material(shader);
            second = new Material(shader);
            construction = new Material(shader);
            renderer.sharedMaterials = new[] { original, second };
            SetBlock(renderer, 13f);
            SetBlock(renderer, 29f, 1);
            Set(presentation, "aircraft", aircraft.transform);
            Set(presentation, "hullRenderers", new Renderer[] { renderer });
            Set(presentation, "shieldDisplay", display);
            Set(presentation, "shield", shield);
            Set(presentation, "levelUi", ui);
            Set(presentation, "constructionMaterial", construction);
            Set(presentation, "traceMaterial", original);
        }

        [TearDown]
        public void TearDown()
        {
            presentation.ResetPresentation();
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(construction);
        }

        [Test]
        public void HullRestorationReturnsEveryMaterialSlotAndPropertyBlock()
        {
            presentation.Begin(false);
            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { construction, construction }));
            Assert.That(display.enabled, Is.False);
            Assert.That(aircraft.activeSelf, Is.False);

            presentation.RestoreHull();

            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { original, second }));
            Assert.That(ReadBlock(renderer), Is.EqualTo(13f));
            Assert.That(ReadBlock(renderer, 1), Is.EqualTo(29f));
            Assert.That(display.enabled, Is.True);
            Assert.That(aircraft.activeSelf, Is.True);
        }

        [Test]
        public void CleanupAfterHullRelinquishPreservesSubsequentDamagePresentation()
        {
            presentation.Begin(false);
            presentation.RestoreHull();
            // The real shield/blink owner is free to replace these after admission.
            renderer.sharedMaterials = new[] { second, second };
            renderer.enabled = false;
            SetBlock(renderer, 47f);

            presentation.ResetPresentation();

            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { second, second }));
            Assert.That(renderer.enabled, Is.False);
            Assert.That(ReadBlock(renderer), Is.EqualTo(47f));
        }

        [Test]
        public void CancelDuringRevealRestoresOriginalDisabledPresentation()
        {
            aircraft.SetActive(false);
            renderer.enabled = false;
            display.enabled = false;
            presentation.Begin(false);

            presentation.ResetPresentation();

            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { original, second }));
            Assert.That(ReadBlock(renderer), Is.EqualTo(13f));
            Assert.That(ReadBlock(renderer, 1), Is.EqualTo(29f));
            Assert.That(renderer.enabled, Is.False);
            Assert.That(display.enabled, Is.False);
            Assert.That(aircraft.activeSelf, Is.False);
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        private static void SetBlock(Renderer target, float value, int slot = -1)
        {
            var block = new MaterialPropertyBlock();
            block.SetFloat(Marker, value);
            if (slot < 0) target.SetPropertyBlock(block); else target.SetPropertyBlock(block, slot);
        }

        private static float ReadBlock(Renderer target, int slot = -1)
        {
            var block = new MaterialPropertyBlock();
            if (slot < 0) target.GetPropertyBlock(block); else target.GetPropertyBlock(block, slot);
            return block.GetFloat(Marker);
        }
    }
}

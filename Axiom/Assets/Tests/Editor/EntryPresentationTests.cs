using System.Reflection;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>The ship's materialization at the reveal seam: the build hands the ship to the shield and hit blink, and
    /// later cleanup never takes it back. The inactive rig avoids scene/input services.</summary>
    public sealed class EntryPresentationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly int Progress = Shader.PropertyToID("_BuildProgress");
        private static readonly int Marker = Shader.PropertyToID("_EntryTestMarker");
        private GameObject rig;
        private GameObject aircraft;
        private MeshRenderer hull, interior;
        private ShieldDisplay display;
        private ShipMaterialization materialization;
        private EntryPresentation presentation;
        private Material original, second;

        [SetUp]
        public void SetUp()
        {
            rig = new GameObject("Entry presentation test");
            rig.SetActive(false);
            aircraft = new GameObject("Aircraft");
            aircraft.transform.SetParent(rig.transform, false);
            hull = new GameObject("Hull").AddComponent<MeshRenderer>();
            hull.transform.SetParent(aircraft.transform, false);
            interior = new GameObject("Interior").AddComponent<MeshRenderer>();
            interior.transform.SetParent(aircraft.transform, false);
            interior.enabled = false;
            materialization = aircraft.AddComponent<ShipMaterialization>();
            Set(materialization, "surfaces", new Renderer[] { hull });
            Set(materialization, "interiors", new Renderer[] { interior });
            Set(materialization, "traces", new Renderer[0]);
            var shield = rig.AddComponent<ShipShield>();
            display = rig.AddComponent<ShieldDisplay>();
            var ui = rig.AddComponent<LevelUi>();
            presentation = rig.AddComponent<EntryPresentation>();
            Shader shader = Shader.Find("GeometricFlight/Grid Construction");
            Assert.That(shader, Is.Not.Null);
            original = new Material(shader);
            second = new Material(shader);
            hull.sharedMaterials = new[] { original, second };
            Set(presentation, "aircraft", aircraft.transform);
            Set(presentation, "materialization", materialization);
            Set(presentation, "shieldDisplay", display);
            Set(presentation, "shield", shield);
            Set(presentation, "levelUi", ui);
            Set(presentation, "traceMaterial", original);
        }

        [TearDown]
        public void TearDown()
        {
            presentation.ResetPresentation();
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void TheBuildHandsTheShipToTheShieldWhenItFinishes()
        {
            presentation.Begin(false);
            Assert.That(materialization.Building, "the ship starts as nothing");
            Assert.That(ReadFloat(hull, Progress), Is.Zero);
            Assert.That(interior.enabled, "the interior shows while building");
            Assert.That(display.enabled, Is.False);
            Assert.That(aircraft.activeSelf, Is.False);

            presentation.RestoreHull();

            Assert.That(materialization.Building, Is.False);
            Assert.That(IsEmpty(hull), "the hull is back on its own materials' values");
            Assert.That(interior.enabled, Is.False, "the interior retires with the build");
            Assert.That(hull.sharedMaterials, Is.EqualTo(new[] { original, second }), "the build never swaps materials");
            Assert.That(display.enabled, Is.True);
            Assert.That(aircraft.activeSelf, Is.True);
        }

        [Test]
        public void CleanupAfterHullRelinquishPreservesSubsequentDamagePresentation()
        {
            presentation.Begin(false);
            presentation.RestoreHull();
            // The real shield/blink owner is free to replace these after admission.
            hull.sharedMaterials = new[] { second, second };
            hull.enabled = false;
            var block = new MaterialPropertyBlock();
            block.SetFloat(Marker, 47f);
            hull.SetPropertyBlock(block);

            presentation.ResetPresentation();

            Assert.That(hull.sharedMaterials, Is.EqualTo(new[] { second, second }));
            Assert.That(hull.enabled, Is.False);
            Assert.That(ReadFloat(hull, Marker), Is.EqualTo(47f));
        }

        [Test]
        public void CancelDuringRevealRestoresOriginalDisabledPresentation()
        {
            aircraft.SetActive(false);
            hull.enabled = false;
            display.enabled = false;
            presentation.Begin(false);

            presentation.ResetPresentation();

            Assert.That(materialization.Building, Is.False, "a cancelled build leaves the finished ship");
            Assert.That(IsEmpty(hull));
            Assert.That(interior.enabled, Is.False);
            Assert.That(hull.enabled, Is.False);
            Assert.That(display.enabled, Is.False);
            Assert.That(aircraft.activeSelf, Is.False);
        }

        [Test]
        public void ARebuildPlaysTheWholeConstructionAgain()
        {
            presentation.Begin(false);
            presentation.RestoreHull();

            presentation.BeginRebuild();
            Assert.That(ReadFloat(hull, Progress), Is.Zero);
            Assert.That(interior.enabled);
            presentation.SampleRebuild(0.5f);
            Assert.That(ReadFloat(hull, Progress), Is.EqualTo(0.5f));
            Assert.That(ReadFloat(interior, Progress), Is.EqualTo(0.5f), "every part reads the same progress");

            presentation.EndRebuild();
            Assert.That(IsEmpty(hull));
            Assert.That(interior.enabled, Is.False);
        }

        [Test]
        public void TheExhaustLightsWithThePermanentSystems()
        {
            var jets = new GameObject("Jets");
            jets.transform.SetParent(aircraft.transform, false);
            Set(materialization, "drive", new[] { jets });
            presentation.Begin(false);
            Assert.That(jets.activeSelf, Is.False, "no exhaust from a ship that isn't there");
            materialization.Show(ShipMaterialization.SystemsArrive - 0.01f);
            Assert.That(jets.activeSelf, Is.False);
            materialization.Show(ShipMaterialization.SystemsArrive);
            Assert.That(jets.activeSelf, "the drive lights with the systems");
            presentation.RestoreHull();
            Assert.That(jets.activeSelf);
        }

        [Test]
        public void TheShipTakesItsAuthoredThreeSecondsFromTheThreshold()
        {
            float seconds = (EntryPresentation.BuildEnd - EntryPresentation.BuildStart)
                * EntrySequence.Duration(EntrySequence.DefaultTempo);
            Assert.That(seconds, Is.EqualTo(3f).Within(0.1f));
            Assert.That(EntryPresentation.BuildStart, Is.EqualTo(EntrySequence.CoverCenter),
                "it starts as the ship crosses the threshold, when the entry first shows it");
            Assert.That(EntryPresentation.BuildEnd, Is.LessThan(0.85f), "with time left for the shields report");
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        private static float ReadFloat(Renderer target, int property)
        {
            var block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            return block.GetFloat(property);
        }

        private static bool IsEmpty(Renderer target)
        {
            var block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            return block.isEmpty;
        }
    }
}

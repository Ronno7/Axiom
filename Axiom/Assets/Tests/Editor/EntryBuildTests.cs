using GeometricFlight.Presentation;
using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Tests
{
    public sealed class EntryBuildTests
    {
        [Test]
        public void SixteenBeatsStayWithinTheEightToTenSecondEntry()
        {
            Assert.That(EntrySequence.Duration(EntrySequence.DefaultTempo), Is.EqualTo(9.4118f).Within(1e-3f));
            Assert.That(EntrySequence.Duration(96f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(EntrySequence.Duration(120f), Is.EqualTo(8f).Within(1e-4f));
        }

        [Test]
        public void FrontsWaitForTheirBeatThenAdvanceAtTheirSpeed()
        {
            float beat = EntrySequence.BeatLength(102f);
            Assert.That(EntrySequence.Front(beat * 4.9f, 102f, 5f, -150f, 95f), Is.EqualTo(float.NegativeInfinity));
            Assert.That(EntrySequence.Front(beat * 5f + 2f, 102f, 5f, -150f, 95f), Is.EqualTo(40f).Within(1e-3f));
        }

        [Test]
        public void TerminalRowsAnnounceConstructionOnTheBeatItStarts()
        {
            float previous = -1f;
            foreach (TerminalLine line in EntrySequence.Script)
            {
                Assert.That(line.Beat, Is.GreaterThan(previous), "rows print in order");
                Assert.That(line.Beat, Is.LessThan(EntrySequence.RetireBeat), "every row prints before rows retire");
                Assert.That(line.Value != null || line.LiveKey != null, Is.True, line.Key + " has no value");
                previous = line.Beat;
            }
            Assert.That(Row("EXTEND").Beat, Is.EqualTo(EntrySequence.MarkingsBeat));
            Assert.That(Row("RAISE").Beat, Is.EqualTo(EntrySequence.RibsBeat));
            Assert.That(Row("CLAD").Beat, Is.EqualTo(EntrySequence.SurfacesBeat));
        }

        [Test]
        public void PassageOutlineIsSymmetricAndRibsMeetAtTheCeilingCentre()
        {
            Vector2[] outline = EnclosedPassage.Outline(64f, 34f, 10f);
            float[] path = EnclosedPassage.PathParameters(outline);
            Assert.That(outline, Has.Length.EqualTo(6));
            Assert.That(outline[0], Is.EqualTo(new Vector2(-32f, 0f)));
            Assert.That(outline[5], Is.EqualTo(new Vector2(32f, 0f)));
            Assert.That(outline[2].y, Is.EqualTo(34f));
            Assert.That(path[0], Is.Zero, "left foot starts drawing");
            Assert.That(path[5], Is.Zero, "right foot starts drawing");
            for (int i = 0; i < 3; i++) Assert.That(path[i], Is.EqualTo(path[5 - i]).Within(1e-5f), "legs rise together");
            float total = 0f;
            for (int i = 1; i < outline.Length; i++) total += Vector2.Distance(outline[i - 1], outline[i]);
            float ceilingHalf = Vector2.Distance(outline[2], outline[3]) * 0.5f;
            Assert.That(path[2] + ceilingHalf / (total * 0.5f), Is.EqualTo(1f).Within(1e-5f), "legs meet at the centre");
        }

        [Test]
        public void RibsSpanThePassageWithoutExceedingTheirSpacing()
        {
            var ribs = EnclosedPassage.RibPositions(-150f, 900f, 24f);
            Assert.That(ribs[0], Is.EqualTo(-150f));
            Assert.That(ribs[ribs.Count - 1], Is.EqualTo(900f));
            for (int i = 1; i < ribs.Count; i++) Assert.That(ribs[i] - ribs[i - 1], Is.InRange(6f, 24f + 1e-3f));
        }

        [Test]
        public void ShadowLightFallsBackToStraightDownUnlessItPointsDownward()
        {
            Assert.That(GroundShadow.LightDirection(Vector3.up), Is.EqualTo(Vector3.down));
            Assert.That(GroundShadow.LightDirection(Vector3.zero), Is.EqualTo(Vector3.down));
            Vector3 slanted = GroundShadow.LightDirection(new Vector3(0f, -1f, 1f));
            Assert.That(slanted.magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(slanted.z, Is.GreaterThan(0f), "a slanted light casts the shadow ahead");
        }

        [Test]
        public void TerminalShowsAtMostSixRowsThenClosesBeforeControl()
        {
            var root = new VisualElement();
            var terminal = new EntryTerminal(root);
            float beat = EntrySequence.BeatLength(102f);
            terminal.Sample(beat * 0.1f, 102f, key => "LIVE");
            Assert.That(terminal.VisibleRows, Is.Zero, "nothing prints before the first row's beat");
            terminal.Sample(beat * 13.5f, 102f, key => "LIVE");
            Assert.That(terminal.VisibleRows, Is.EqualTo(EntrySequence.VisibleRows));
            StringAssert.StartsWith("THROTTLE", terminal.NewestText);
            terminal.Sample(beat * 15.9f, 102f, key => "LIVE");
            Assert.That(terminal.VisibleRows, Is.Zero, "rows retire before the frame collapses");
            terminal.Sample(beat * EntrySequence.Beats, 102f, key => "LIVE");
            Assert.That(terminal.VisibleRows, Is.Zero);
        }

        [Test]
        public void CodeWindowsStreamOneAtATimeAndFinishBeforeClosing()
        {
            var ends = new System.Collections.Generic.List<Vector2>();
            foreach (EntryCodeTerminals.Window w in EntryCodeTerminals.Script)
            {
                float[] starts = EntryCodeTerminals.LineStarts(w, 102f, key => "live value");
                float beat = EntrySequence.BeatLength(102f);
                string last = w.Lines[w.Lines.Length - 1];
                int bar = last.LastIndexOf('|');
                float typed = (bar < 0 ? last.Length : bar) / EntryCodeTerminals.CharactersPerSecond / beat;
                float end = starts[starts.Length - 1] + typed;
                Assert.That(end, Is.LessThan(w.CloseBeat), w.Title + " still typing when it closes");
                Assert.That(w.CloseBeat, Is.LessThanOrEqualTo(EntrySequence.Beats), w.Title + " outlives the entry");
                ends.Add(new Vector2(w.StreamBeat, end));
            }
            ends.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = 1; i < ends.Count; i++)
                Assert.That(ends[i].x, Is.GreaterThanOrEqualTo(ends[i - 1].y), "two windows stream at once");
        }

        [Test]
        public void CodeWindowsShowOnlyResolvedValues()
        {
            var root = new VisualElement();
            var windows = new EntryCodeTerminals(root, null);
            for (float beat = 0f; beat < EntrySequence.Beats; beat += 0.25f)
                windows.Sample(beat * EntrySequence.BeatLength(102f), 102f, key => "v");
            foreach (Label label in root.Query<Label>().ToList())
                StringAssert.DoesNotContain("{", label.text, "unresolved token in " + label.text);
        }

        private static TerminalLine Row(string key)
        {
            foreach (TerminalLine line in EntrySequence.Script) if (line.Key == key) return line;
            throw new System.ArgumentException(key);
        }
    }
}

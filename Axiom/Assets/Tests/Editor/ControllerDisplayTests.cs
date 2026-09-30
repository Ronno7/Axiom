using GeometricFlight.Input;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class ControllerDisplayTests
    {
        private static string Body(string framed) => framed.Substring(0, framed.LastIndexOf('*'));

        // ---------- Row encoding ----------

        [Test]
        public void RowIsFramedAndTrailingSpacesDropped()
        {
            string row = ControllerProtocol.FormatLcdRow(1, "HELLO");
            Assert.That(Body(row), Is.EqualTo("LT,1,HELLO"));
            Assert.That(row.Substring(row.Length - 2), Is.EqualTo(ControllerProtocol.Checksum("LT,1,HELLO").ToString("X2")));
        }

        [Test]
        public void GlyphsArrowsAndBlocksAreEscaped()
        {
            string cells = $"{LcdGlyph.Bar1}{LcdGlyph.Target}{LcdGlyph.Right}{LcdGlyph.Left}{LcdGlyph.Full}\\A";
            Assert.That(Body(ControllerProtocol.FormatLcdRow(0, cells)), Is.EqualTo(@"LT,0,\0\7\>\<\#\\A"));
        }

        [Test]
        public void UnsupportedCharactersBecomeQuestionMarksAndRowsAreCut()
        {
            Assert.That(Body(ControllerProtocol.FormatLcdRow(0, "é")), Is.EqualTo("LT,0,?"));
            Assert.That(Body(ControllerProtocol.FormatLcdRow(0, "0123456789ABCDEFGHIJ")), Is.EqualTo("LT,0,0123456789ABCDEF"));
        }

        // ---------- Text helpers ----------

        [Test]
        public void CenterAndSpreadFillOneRow()
        {
            Assert.That(LcdGlyph.Center("PAUSED"), Is.EqualTo("     PAUSED     "));
            Assert.That(LcdGlyph.Spread("TIME", "0:12.3"), Is.EqualTo("TIME      0:12.3"));
        }

        [Test]
        public void BarFillsFiveStepsPerCell()
        {
            Assert.That(LcdGlyph.Bar(0f, 4), Is.EqualTo("    "));
            Assert.That(LcdGlyph.Bar(1f, 4), Is.EqualTo(new string(LcdGlyph.Full, 4)));
            // 0.4 of 4 cells = 8 of 20 steps: one full cell, then 3 of 5.
            Assert.That(LcdGlyph.Bar(0.4f, 4), Is.EqualTo($"{LcdGlyph.Full}{LcdGlyph.Bar3}  "));
        }

        [Test]
        public void BarMarksEmptyCellsInsideTheBand()
        {
            string bar = LcdGlyph.Bar(0.1f, 10, 0.6f, 0.8f);
            Assert.That(bar, Is.EqualTo($"{LcdGlyph.Full}     {LcdGlyph.Band}{LcdGlyph.Band}  "));
        }

        // ---------- Send policy ----------

        [Test]
        public void SendsEachChangedRowOncePacedByInterval()
        {
            var display = new ControllerDisplay();
            display.Show("TOP", "BOTTOM");
            Assert.That(Body(display.Next(0.0)), Is.EqualTo("LT,0,TOP"));
            Assert.That(display.Next(0.01), Is.Null, "one row per interval");
            Assert.That(Body(display.Next(0.06)), Is.EqualTo("LT,1,BOTTOM"));
            Assert.That(display.Next(0.2), Is.Null, "nothing changed");

            display.Show("TOP", "CHANGED");
            Assert.That(Body(display.Next(0.3)), Is.EqualTo("LT,1,CHANGED"));
        }

        [Test]
        public void ResendsUnchangedRowsAsKeepAliveAndAfterInvalidate()
        {
            var display = new ControllerDisplay();
            display.Show("A", "B");
            display.Next(0.0);
            display.Next(0.1);
            Assert.That(display.Next(0.5), Is.Null);
            Assert.That(display.Next(1.05), Is.Not.Null, "keep-alive");

            display.Invalidate();
            Assert.That(display.Next(1.06), Is.Not.Null, "resent at once after invalidate");
        }

        [Test]
        public void FlashOverridesScreensUntilItExpires()
        {
            var display = new ControllerDisplay();
            display.Show("SCREEN", "");
            display.Flash("LINK", "COM3", 1.0);
            Assert.That(Body(display.Next(0.0)), Is.EqualTo("LT,0,LINK"));
            Assert.That(Body(display.Next(0.1)), Is.EqualTo("LT,1,COM3"));
            Assert.That(Body(display.Next(1.1)), Is.EqualTo("LT,0,SCREEN"));
        }
    }
}

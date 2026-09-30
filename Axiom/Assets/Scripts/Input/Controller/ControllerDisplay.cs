using System.Text;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>Characters for the controller's 16×2 LCD. The first eight are the custom glyphs defined
    /// in the firmware (the character code is the CGRAM slot); the rest are built into the display.</summary>
    public static class LcdGlyph
    {
        public const int Columns = 16;

        public const char Bar1 = '\u0000', Bar2 = '\u0001', Bar3 = '\u0002', Bar4 = '\u0003';
        /// <summary>Empty cell inside a recommended throttle band (rails top and bottom).</summary>
        public const char Band = '\u0004';
        public const char ShieldUp = '\u0005', ShieldDown = '\u0006';
        /// <summary>Octahedron, like the course targets.</summary>
        public const char Target = '\u0007';
        public const char Full = '█', Right = '→', Left = '←';

        /// <summary>Exactly one row: padded with spaces or cut.</summary>
        public static string Fit(string text)
        {
            text ??= "";
            return text.Length >= Columns ? text.Substring(0, Columns) : text.PadRight(Columns);
        }

        public static string Center(string text)
        {
            text ??= "";
            return text.Length >= Columns ? Fit(text) : Fit(new string(' ', (Columns - text.Length) / 2) + text);
        }

        /// <summary>Left text and right text on one row, with the gap between them.</summary>
        public static string Spread(string left, string right)
        {
            int gap = Mathf.Max(1, Columns - left.Length - right.Length);
            return Fit(left + new string(' ', gap) + right);
        }

        /// <summary>A bar with 5 steps per cell. Empty cells that overlap [bandFrom, bandTo] (fractions
        /// of the bar) show the band glyph, so the target region is visible before the fill reaches it.</summary>
        public static string Bar(float fraction, int cells, float bandFrom = 1f, float bandTo = 0f)
        {
            int pixels = Mathf.RoundToInt(Mathf.Clamp01(fraction) * cells * 5);
            var bar = new StringBuilder(cells);
            for (int i = 0; i < cells; i++)
            {
                int fill = Mathf.Clamp(pixels - i * 5, 0, 5);
                // A cell touching the band only at its edge is outside (with float tolerance).
                bool inBand = (i + 1f) / cells > bandFrom + 1e-4f && (float)i / cells < bandTo - 1e-4f;
                bar.Append(fill == 5 ? Full : fill > 0 ? (char)(Bar1 + fill - 1) : inBand ? Band : ' ');
            }
            return bar.ToString();
        }
    }

    /// <summary>
    /// What the controller LCD should show, and when to send it. Screens call Show every frame; a row
    /// is sent only when it changes, one row per SendInterval (small bursts keep the Uno's 64-byte
    /// receive buffer safe), and resent every KeepAlive so the firmware knows the game still drives
    /// the display. After 3 s without rows the firmware returns to its standby screen.
    /// </summary>
    public sealed class ControllerDisplay
    {
        public const double SendInterval = 0.05;
        public const double KeepAlive = 1.0;

        private readonly string[] wanted = { LcdGlyph.Center("AXIOM"), LcdGlyph.Fit("") };
        private readonly string[] flash = new string[2];
        private readonly string[] sent = new string[2];
        private readonly double[] sentAt = new double[2];
        private double flashUntil = double.NegativeInfinity;
        private double lastSend = double.NegativeInfinity;
        private int nextRow;

        public string Top => wanted[0];
        public string Bottom => wanted[1];

        public void Show(string top, string bottom)
        {
            wanted[0] = LcdGlyph.Fit(top);
            wanted[1] = LcdGlyph.Fit(bottom);
        }

        /// <summary>Overrides whatever screens show until <paramref name="until"/>.</summary>
        public void Flash(string top, string bottom, double until)
        {
            flash[0] = LcdGlyph.Fit(top);
            flash[1] = LcdGlyph.Fit(bottom);
            flashUntil = until;
        }

        /// <summary>Forgets what was sent, so every row goes out again (the Uno resets on connect).</summary>
        public void Invalidate()
        {
            sent[0] = sent[1] = null;
            lastSend = double.NegativeInfinity;
        }

        /// <summary>The framed command to send now, or null if nothing is due.</summary>
        public string Next(double now)
        {
            if (now - lastSend < SendInterval) return null;
            string[] rows = now < flashUntil ? flash : wanted;
            for (int i = 0; i < 2; i++)
            {
                int row = (nextRow + i) % 2;
                if (rows[row] == sent[row] && now - sentAt[row] < KeepAlive) continue;
                sent[row] = rows[row];
                sentAt[row] = now;
                lastSend = now;
                nextRow = 1 - row;
                return ControllerProtocol.FormatLcdRow(row, rows[row]);
            }
            return null;
        }
    }
}

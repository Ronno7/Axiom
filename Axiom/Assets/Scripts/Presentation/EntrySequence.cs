using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>One terminal row: when it prints, what it says, and whether it reports the Grid's own
    /// work (geometry it is about to build) or the player's craft (real state).</summary>
    public readonly struct TerminalLine
    {
        public readonly float Beat;
        public readonly string Key;
        public readonly string Value;
        public readonly bool Craft;
        /// <summary>Live rows take their value from current state every frame (see EntryTerminal).</summary>
        public readonly string LiveKey;

        public TerminalLine(float beat, string key, string value, bool craft, string liveKey = null)
        {
            Beat = beat;
            Key = key;
            Value = value;
            Craft = craft;
            LiveKey = liveKey;
        }
    }

    /// <summary>
    /// The shared entry as a four-bar build: sixteen beats from PLAY to flight control. Every visual
    /// accent sits on this grid so the temporary score and the picture agree, but construction flows
    /// between accents. Times are seconds from PLAY at the given tempo; nothing here owns a clock.
    /// </summary>
    public static class EntrySequence
    {
        public const float DefaultTempo = 102f;
        public const int Beats = 16;
        /// <summary>The signal cover peaks on bar 3's downbeat, halfway through.</summary>
        public const float CoverCenter = 0.5f;

        // Construction follows the systems readout: each part starts behind the camera on the beat its row prints,
        // so it forms around the viewer as it is announced, then races ahead and finishes during the first
        // seconds of flight. The edge lines leave with the ribs and overtake them.
        public const float MarkingsBeat = 4.8f, RibsBeat = 5.6f, SurfacesBeat = 6.4f;
        public const float MarkingSpeed = 140f, RibSpeed = 95f, SurfaceSpeed = 95f, EdgeSpeed = 190f;

        public const float RetireBeat = 14f, CollapseBeat = 15f;
        /// <summary>Terminals wait until the menu has flown past the camera and the build is about to start.</summary>
        public const float TerminalsBeat = 4f;

        public static float BeatLength(float tempo) => 60f / Mathf.Clamp(tempo, 60f, 200f);
        public static float Duration(float tempo) => Beats * BeatLength(tempo);

        /// <summary>World Z reached by a front that starts at <paramref name="originZ"/> on a beat.</summary>
        public static float Front(float elapsed, float tempo, float startBeat, float originZ, float speed)
        {
            float start = startBeat * BeatLength(tempo);
            return elapsed < start ? float.NegativeInfinity : originZ + (elapsed - start) * speed;
        }

        /// <summary>Rows printed in order. Craft rows state real values; Grid rows announce geometry
        /// that appears as they print. No progress figures, scans or admission ceremony.</summary>
        public static readonly TerminalLine[] Script =
        {
            new TerminalLine(4.2f, "ALLOCATE", null, false, "passage"),
            new TerminalLine(MarkingsBeat, "EXTEND", "FLOOR SEAMS", false),
            new TerminalLine(RibsBeat, "RAISE", "RIBS / 24 M", false),
            new TerminalLine(SurfacesBeat, "CLAD", "WALLS + CANOPY", false),
            new TerminalLine(7.2f, "ROUTE", null, false, "route"),
            new TerminalLine(8.6f, "RENDER", "CRAFT", true),
            new TerminalLine(9.5f, "INPUT", null, true, "input"),
            new TerminalLine(10.5f, "HANDLING", null, true, "handling"),
            new TerminalLine(11.5f, "SHIELD", null, true, "shield"),
            new TerminalLine(12f, "MASTER ARM", null, true, "arm"),
            new TerminalLine(13f, "THROTTLE", null, true, "throttle"),
        };

        public const int VisibleRows = 3;
        public const float CharactersPerSecond = 70f;
    }
}

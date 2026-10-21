namespace GeometricFlight.Presentation
{
    /// <summary>One frame of the menu's signal-glitch event. Amounts are 0..1 unless noted; zero means off.</summary>
    public readonly struct GlitchFrame
    {
        public readonly float Tear, Blocks, EchoPixels, EchoAmount, Scanlines, Fringe;
        /// <summary>Roll bar position, 0 = top of the screen, 1 = bottom; negative = no bar.</summary>
        public readonly float RollY;
        /// <summary>Construct snap offset in metres.</summary>
        public readonly float SnapX, SnapY;
        public readonly float TitleTear, TitleEchoPixels;
        public readonly bool Scramble, Drop;
        public readonly int Seed;

        public GlitchFrame(float tear, int seed, float echoPixels, float echoAmount, float blocks, float scanlines,
            float rollY, float fringe, float snapX, float snapY, float titleTear, float titleEchoPixels, bool scramble, bool drop)
        {
            Tear = tear; Seed = seed; EchoPixels = echoPixels; EchoAmount = echoAmount; Blocks = blocks;
            Scanlines = scanlines; RollY = rollY; Fringe = fringe; SnapX = snapX; SnapY = snapY;
            TitleTear = titleTear; TitleEchoPixels = titleEchoPixels; Scramble = scramble; Drop = drop;
        }

        public static GlitchFrame Calm => new GlitchFrame(0f, 0, 0f, 0f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, 0f, false, false);
    }

    /// <summary>The glitch event, frame by frame: onset with a roll bar and a faint echo, a peak with tears, a
    /// construct drop-out and a scrambled status line, an aftershock, then the roll bar crossing the UI.</summary>
    public static class MenuGlitchTimeline
    {
        public const int PeakFrame = 5;

        private static readonly GlitchFrame[] Frames =
        {
            //              tear   seed echo  amt   blocks scan   roll   fringe snapX  snapY  tTear  tEcho scramble drop
            GlitchFrame.Calm,
            GlitchFrame.Calm,
            new GlitchFrame(0f,    2,   0f,   0f,   0f,    0.2f,  0.04f, 0.05f, 0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0.15f, 3,   4f,   0.5f, 0f,    0.35f, 0.11f, 0.15f, 0f,    0f,    0f,    3f,  false, false),
            new GlitchFrame(0.4f,  4,   7f,   0.8f, 0.2f,  0.5f,  0.18f, 0.25f, 0.3f,  0f,    0.35f, 7f,  false, false),
            new GlitchFrame(0.6f,  5,   11f,  0.9f, 0.4f,  0.7f,  0.25f, 0.3f,  -0.2f, 0.1f,  0.55f, 10f, true,  true),
            new GlitchFrame(0.3f,  6,   6f,   0.6f, 0.1f,  0.45f, 0.32f, 0.2f,  0f,    0f,    0.2f,  5f,  true,  true),
            new GlitchFrame(0.12f, 7,   3f,   0.4f, 0f,    0.3f,  0.39f, 0.1f,  0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0.25f, 8,   5f,   0.5f, 0.1f,  0.35f, 0.46f, 0.15f, 0.1f,  0f,    0.2f,  4f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0.15f, 0.53f, 0.08f, 0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0.1f,  0.6f,  0f,    0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0f,    0.67f, 0f,    0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0f,    0.74f, 0f,    0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0f,    0.81f, 0f,    0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0f,    0.88f, 0f,    0f,    0f,    0f,    0f,  false, false),
            new GlitchFrame(0f,    9,   0f,   0f,   0f,    0f,    0.95f, 0f,    0f,    0f,    0f,    0f,  false, false),
            GlitchFrame.Calm,
            GlitchFrame.Calm,
        };

        public static int FrameCount => Frames.Length;

        /// <summary>The frame at an index; calm outside the event.</summary>
        public static GlitchFrame Frame(int index) =>
            index >= 0 && index < Frames.Length ? Frames[index] : GlitchFrame.Calm;

        /// <summary>Replaces about 45% of the non-space characters with signal glyphs; same seed, same result.</summary>
        public static string Scramble(string text, int seed)
        {
            if (string.IsNullOrEmpty(text)) return text;
            const string glyphs = "#/\\_-=<>01";
            var random = new System.Random(seed * 31 + 7);
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] != ' ' && random.NextDouble() < 0.45) chars[i] = glyphs[random.Next(glyphs.Length)];
            return new string(chars);
        }

        /// <summary>Seconds until the next event, uniform in [min, max] (the two may come in either order).</summary>
        public static float NextInterval(System.Random random, float min, float max)
        {
            if (max < min) (min, max) = (max, min);
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}

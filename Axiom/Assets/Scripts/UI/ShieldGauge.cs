using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>The shield as forward-leaning segments, in the same lean as the menu buttons and key chips. A lost segment
    /// flashes hot and empties; restored ones refill in a left to right sweep; the last one burns Ember. Reads a count
    /// and owns no rules.</summary>
    public sealed class ShieldGauge
    {
        private const float FlashSeconds = 0.45f, SweepDelay = 0.09f;
        private readonly Segment[] segments;
        private int shown = -1;

        public ShieldGauge(VisualElement container, int count)
        {
            segments = new Segment[count];
            for (int i = 0; i < count; i++)
            {
                segments[i] = new Segment();
                container.Add(segments[i]);
            }
        }

        public void Show(int current)
        {
            if (current == shown) return;
            bool first = shown < 0;
            for (int i = 0; i < segments.Length; i++)
            {
                bool lit = i < current;
                if (lit != segments[i].Lit && !first)
                    segments[i].Flash(lit ? AxiomPalette.Light : AxiomPalette.Hot,
                        lit ? -(i - Mathf.Max(0, shown)) * SweepDelay : 0f);
                segments[i].Lit = lit;
            }
            bool low = current == 1;
            foreach (Segment segment in segments) segment.Low = low;
            shown = current;
        }

        public void Tick(float deltaTime)
        {
            foreach (Segment segment in segments) segment.Tick(deltaTime);
        }

        private sealed class Segment : VisualElement
        {
            private const float Lean = 7f, Stroke = 2f;
            private Color flashColour;
            private float flash; // Seconds into the flash; negative while it waits its turn in a sweep.
            private bool flashing, lit, low;

            public Segment()
            {
                pickingMode = PickingMode.Ignore;
                style.width = 62f;
                style.height = 18f;
                style.marginRight = 5f;
                generateVisualContent = Draw;
            }

            public bool Lit
            {
                get => lit;
                set { if (value != lit) { lit = value; MarkDirtyRepaint(); } }
            }

            public bool Low
            {
                set { if (value != low) { low = value; MarkDirtyRepaint(); } }
            }

            public void Flash(Color colour, float startAt)
            {
                flashColour = colour;
                flash = startAt;
                flashing = true;
                MarkDirtyRepaint();
            }

            public void Tick(float deltaTime)
            {
                if (!flashing) return;
                flash += deltaTime;
                flashing = flash < FlashSeconds;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (w <= 0f || h <= 0f) return;
                float i = Stroke * 0.5f;
                Painter2D painter = context.painter2D;
                painter.lineWidth = Stroke;
                painter.BeginPath();
                painter.MoveTo(new Vector2(Lean + i, i));
                painter.LineTo(new Vector2(w - i, i));
                painter.LineTo(new Vector2(w - Lean - i, h - i));
                painter.LineTo(new Vector2(i, h - i));
                painter.ClosePath();

                Color rest = !lit ? AxiomPalette.Clear : low ? AxiomPalette.Ember : AxiomPalette.Cyan;
                // A refill waits its turn showing empty; once flashing, the colour settles to the segment's own.
                bool waiting = flashing && flash < 0f;
                float t = flashing && !waiting ? 1f - flash / FlashSeconds : 0f;
                Color fill = waiting ? AxiomPalette.Clear : Color.Lerp(rest, flashColour, t * t);
                if (fill.a > 0f)
                {
                    painter.fillColor = fill;
                    painter.Fill();
                }
                // An empty slot keeps enough outline to hold the count of three.
                painter.strokeColor = fill.a > 0f ? fill : AxiomPalette.Muted;
                painter.Stroke();
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Meta UI: a single angular screen-edge impact pulse, with a clear centre.
    /// Driven only by accepted shield hits. No persistent low-health flashing.</summary>
    [UxmlElement]
    public partial class DamageFeedback : VisualElement
    {
        private const float Duration = 0.55f;
        private float remaining;
        private bool fatal;

        public DamageFeedback()
        {
            pickingMode = PickingMode.Ignore;
            focusable = false;
            generateVisualContent += Draw;
        }

        public void Pulse(bool depleted)
        {
            fatal = depleted;
            remaining = Duration;
            MarkDirtyRepaint();
        }

        public void Tick(float deltaTime)
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - deltaTime);
            MarkDirtyRepaint();
        }

        public void ResetPulse()
        {
            remaining = 0f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (remaining <= 0f || contentRect.width <= 0f || contentRect.height <= 0f) return;
            float life = remaining / Duration;
            float strength = life * life;
            float w = contentRect.width, h = contentRect.height;
            float depth = Mathf.Min(w, h) * Mathf.Lerp(0.018f, 0.065f, strength);
            Painter2D p = context.painter2D;
            // Dark outer facets stay legible over bright scenery; the pale inner line reads on black.
            p.fillColor = new Color(0.22f, 0.035f, 0.025f, strength * (fatal ? 0.72f : 0.52f));
            p.strokeColor = new Color(1f, 0.88f, 0.8f, strength * 0.85f);
            p.lineWidth = 2f;
            Edge(p, new Vector2(0, 0), new Vector2(0, h), new Vector2(depth, 0));
            Edge(p, new Vector2(w, h), new Vector2(w, 0), new Vector2(-depth, 0));
            Edge(p, new Vector2(w, 0), new Vector2(0, 0), new Vector2(0, depth));
            Edge(p, new Vector2(0, h), new Vector2(w, h), new Vector2(0, -depth));
        }

        private static void Edge(Painter2D p, Vector2 start, Vector2 end, Vector2 inward)
        {
            Vector2 along = end - start;
            p.BeginPath();
            p.MoveTo(start);
            p.LineTo(end);
            p.LineTo(start + along * 0.8f + inward);
            p.LineTo(start + along * 0.5f + inward * 0.55f);
            p.LineTo(start + along * 0.2f + inward);
            p.ClosePath();
            p.Fill();
            p.BeginPath();
            p.MoveTo(start + along * 0.2f + inward);
            p.LineTo(start + along * 0.5f + inward * 0.55f);
            p.LineTo(start + along * 0.8f + inward);
            p.Stroke();
        }
    }
}

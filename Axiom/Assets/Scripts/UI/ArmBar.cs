using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>MASTER ARM's bar: diagonal hatching while SAFE, solid while ARMED, so the shape carries it as well as
    /// the colour.</summary>
    [UxmlElement]
    public partial class ArmBar : VisualElement
    {
        private const float Spacing = 18f;
        private bool solid;
        private Color colour = AxiomPalette.Text;

        public ArmBar() => generateVisualContent = Draw;

        public void Set(bool isSolid, Color newColour)
        {
            if (isSolid == solid && newColour == colour) return;
            solid = isSolid;
            colour = newColour;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            Painter2D painter = context.painter2D;
            painter.strokeColor = colour;
            painter.fillColor = colour;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(1f, 1f));
            painter.LineTo(new Vector2(w - 1f, 1f));
            painter.LineTo(new Vector2(w - 1f, h - 1f));
            painter.LineTo(new Vector2(1f, h - 1f));
            painter.ClosePath();
            if (solid) painter.Fill();
            else painter.Stroke();
            if (solid) return;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            for (float x = 1f; x + h < w; x += Spacing)
            {
                painter.MoveTo(new Vector2(x, h - 1f));
                painter.LineTo(new Vector2(x + h - 2f, 1f));
            }
            painter.Stroke();
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>
    /// Button drawn as a forward-leaning parallelogram ending in a chevron point on the right,
    /// so menus lean into the direction of flight. Shape and colors come from USS custom
    /// properties, so :hover and :focus rules restyle it like any other element:
    /// --slant-fill, --slant-stroke, --slant-stroke-width, --slant-lean, --slant-point.
    /// </summary>
    [UxmlElement]
    public partial class SlantButton : Button
    {
        private static readonly CustomStyleProperty<Color> FillProperty = new CustomStyleProperty<Color>("--slant-fill");
        private static readonly CustomStyleProperty<Color> StrokeProperty = new CustomStyleProperty<Color>("--slant-stroke");
        private static readonly CustomStyleProperty<float> StrokeWidthProperty = new CustomStyleProperty<float>("--slant-stroke-width");
        private static readonly CustomStyleProperty<float> LeanProperty = new CustomStyleProperty<float>("--slant-lean");
        private static readonly CustomStyleProperty<float> PointProperty = new CustomStyleProperty<float>("--slant-point");

        private Color fill = Color.clear;
        private Color stroke = Color.white;
        private float strokeWidth = 2f;
        private float lean = 18f;
        private float point = 20f;

        public SlantButton()
        {
            // Draw the shape first so the button's own text renders on top of it.
            System.Action<MeshGenerationContext> text = generateVisualContent;
            generateVisualContent = Draw;
            if (text != null) generateVisualContent += text;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
        }

        private void OnCustomStyle(CustomStyleResolvedEvent e)
        {
            ICustomStyle style = e.customStyle;
            if (style.TryGetValue(FillProperty, out Color f)) fill = f;
            if (style.TryGetValue(StrokeProperty, out Color s)) stroke = s;
            if (style.TryGetValue(StrokeWidthProperty, out float w)) strokeWidth = w;
            if (style.TryGetValue(LeanProperty, out float l)) lean = l;
            if (style.TryGetValue(PointProperty, out float p)) point = p;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            float i = strokeWidth * 0.5f;

            // Unleaned outline: left edge at x = 0, chevron tip at the right, then shear so the
            // top shifts right by `lean` (forward lean).
            Painter2D painter = context.painter2D;
            painter.lineWidth = strokeWidth;
            painter.lineJoin = LineJoin.Miter;
            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.BeginPath();
            painter.MoveTo(new Vector2(lean + i, i));
            painter.LineTo(new Vector2(w - point - i, i));
            painter.LineTo(new Vector2(w - lean * 0.5f - i, h * 0.5f));
            painter.LineTo(new Vector2(w - lean - point - i, h - i));
            painter.LineTo(new Vector2(i, h - i));
            painter.ClosePath();
            if (fill.a > 0f) painter.Fill();
            if (strokeWidth > 0f && stroke.a > 0f) painter.Stroke();
        }
    }
}

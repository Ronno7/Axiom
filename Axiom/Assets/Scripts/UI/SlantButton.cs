using GeometricFlight.Presentation;
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
        private Color stroke = AxiomPalette.Light;
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
            RegisterCallback<GeometryChangedEvent>(_ => AlignFocusMarker());
            // The mouse selects the way the arrow keys do: hovering focuses, so both light the button the same
            // way and only one button is ever lit.
            RegisterCallback<PointerEnterEvent>(_ => { if (enabledInHierarchy && focusable) Focus(); });
        }

        private void OnCustomStyle(CustomStyleResolvedEvent e)
        {
            ICustomStyle style = e.customStyle;
            if (style.TryGetValue(FillProperty, out Color f)) fill = f;
            if (style.TryGetValue(StrokeProperty, out Color s)) stroke = s;
            if (style.TryGetValue(StrokeWidthProperty, out float w)) strokeWidth = w;
            if (style.TryGetValue(LeanProperty, out float l)) lean = l;
            if (style.TryGetValue(PointProperty, out float p)) point = p;
            AlignFocusMarker();
            MarkDirtyRepaint();
        }

        private void AlignFocusMarker()
        {
            VisualElement marker = this.Q<VisualElement>(className: "focus-marker");
            float edgeHeight = layout.height - strokeWidth;
            if (marker == null || edgeHeight <= 0f || float.IsNaN(edgeHeight)) return;
            // Match the drawn left edge, including its stroke inset. Rotation keeps the marker's
            // centre and thickness while its top leans right with the button at every button height.
            float angle = Mathf.Atan2(lean, edgeHeight) * Mathf.Rad2Deg;
            marker.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            float i = strokeWidth * 0.5f;

            var corners = new[]
            {
                new Vector2(lean + i, i), new Vector2(w - point - i, i),
                new Vector2(w - lean * 0.5f - i, h * 0.5f),
                new Vector2(w - lean - point - i, h - i), new Vector2(i, h - i)
            };
            // A direct fill mesh stays below the native Button caption. In Unity 6000.3 the
            // Painter2D fill can cover that caption even when Draw is the first callback.
            if (fill.a > 0f)
            {
                MeshWriteData mesh = context.Allocate(5, 9);
                foreach (Vector2 corner in corners)
                    mesh.SetNextVertex(new Vertex { position = new Vector3(corner.x, corner.y, Vertex.nearZ), tint = fill });
                for (ushort triangle = 1; triangle < 4; triangle++)
                {
                    mesh.SetNextIndex(0);
                    mesh.SetNextIndex(triangle);
                    mesh.SetNextIndex((ushort)(triangle + 1));
                }
            }

            // Unleaned outline: left edge at x = 0, chevron tip at the right, then shear so the
            // top shifts right by `lean` (forward lean).
            Painter2D painter = context.painter2D;
            painter.lineWidth = strokeWidth;
            painter.lineJoin = LineJoin.Miter;
            painter.strokeColor = stroke;
            painter.BeginPath();
            painter.MoveTo(corners[0]);
            for (int corner = 1; corner < corners.Length; corner++) painter.LineTo(corners[corner]);
            painter.ClosePath();
            if (strokeWidth > 0f && stroke.a > 0f) painter.Stroke();
        }
    }
}

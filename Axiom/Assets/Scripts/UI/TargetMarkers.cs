using GeometricFlight.Combat;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>What sits on the world: gun, acquiring and locked brackets, one family of corners, and the SAFE mark. While
    /// SAFE, an X and the word sit on the aim point: normal flight stays quiet, and the exception shows where the eye
    /// already is. Only an armed gun brackets its target.</summary>
    public sealed class TargetMarkers
    {
        private const int GunMarker = 9, CandidateMarker = 8;
        private const float SafeMarkSize = 58f;
        private static readonly Color Cyan = AxiomPalette.Cyan;
        private static readonly Color Ready = AxiomPalette.Light;

        private readonly VisualElement root, layer;
        private readonly VisualElement[] markers = new VisualElement[10];
        private readonly Label[] numbers = new Label[10];
        private readonly Cross safeMark;
        private readonly Label safeWord;

        public TargetMarkers(VisualElement root)
        {
            this.root = root;
            layer = Element("target-layer");
            root.Add(layer);
            safeMark = new Cross(2.5f) { pickingMode = PickingMode.Ignore };
            safeMark.AddToClassList("safe-mark");
            layer.Add(safeMark);
            safeWord = new Label("SAFE") { pickingMode = PickingMode.Ignore };
            safeWord.AddToClassList("safe-word");
            layer.Add(safeWord);
            for (int i = 0; i < markers.Length; i++)
            {
                VisualElement marker = Element("target-marker");
                for (int c = 0; c < 4; c++)
                {
                    VisualElement corner = Element("target-corner");
                    corner.AddToClassList((c & 1) == 0 ? "target-corner--left" : "target-corner--right");
                    corner.AddToClassList((c & 2) == 0 ? "target-corner--top" : "target-corner--bottom");
                    marker.Add(corner);
                }
                numbers[i] = new Label { pickingMode = PickingMode.Ignore };
                numbers[i].AddToClassList("target-number");
                marker.Add(numbers[i]);
                markers[i] = marker;
                layer.Add(marker);
            }
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        public void Remove() => layer.RemoveFromHierarchy();

        /// <summary>The X and the word on the aim point, or neither.</summary>
        public void ShowSafeMark(bool show, Camera view, Vector3 aimPoint, Color colour)
        {
            Vector3 point = show ? view.WorldToScreenPoint(aimPoint) : Vector3.zero;
            show &= point.z > 0f;
            safeMark.style.display = safeWord.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            Vector2 at = UiScreens.ScreenToPanel(point, root);
            safeMark.style.left = at.x - SafeMarkSize * 0.5f;
            safeMark.style.top = at.y - SafeMarkSize * 0.5f;
            safeMark.Colour = colour;
            safeWord.style.left = at.x + SafeMarkSize * 0.5f + 8f;
            safeWord.style.top = at.y - 16f;
            safeWord.style.color = colour;
        }

        public void HideBrackets()
        {
            foreach (VisualElement marker in markers) marker.style.display = DisplayStyle.None;
        }

        public void ShowBrackets(LockOnWeapon weapon, ShootableTarget gunTarget, Camera view)
        {
            MissileLockState state = weapon.State;
            ShowMarker(GunMarker, gunTarget, Cyan, string.Empty, view, 44f);
            for (int i = 0; i < state.Locks.Count; i++)
                ShowMarker(i, weapon.Target(state.Locks[i]), Ready, "LOCK " + (i + 1), view, 48f);
            // Acquiring closes in on the target as it completes.
            ShowMarker(CandidateMarker, weapon.Target(state.CandidateId), Color.Lerp(Cyan * 0.55f, Ready, state.Progress),
                "ACQUIRE " + Mathf.RoundToInt(state.Progress * 100f) + "%", view, Mathf.Lerp(80f, 48f, state.Progress));
        }

        private void ShowMarker(int index, ShootableTarget target, Color color, string text, Camera view, float size)
        {
            if (target == null || target.IsDestroyed || view == null) return;
            Vector3 point = view.WorldToScreenPoint(target.transform.position);
            if (point.z <= 0f || point.x < 0f || point.x > Screen.width || point.y < 0f || point.y > Screen.height) return;
            VisualElement marker = markers[index];
            Vector2 at = UiScreens.ScreenToPanel(point, root);
            marker.style.display = DisplayStyle.Flex;
            marker.style.width = marker.style.height = size;
            marker.style.left = at.x - size * 0.5f;
            marker.style.top = at.y - size * 0.5f;
            foreach (VisualElement corner in marker.Children())
            {
                corner.style.borderLeftColor = corner.style.borderRightColor = color;
                corner.style.borderTopColor = corner.style.borderBottomColor = color;
            }
            // Beside the corners, never below them, where the aim diamonds usually sit.
            numbers[index].style.left = size + 6f;
            numbers[index].style.top = size * 0.5f - 9f;
            numbers[index].style.color = color;
            numbers[index].text = text;
        }

        /// <summary>An X corner to corner: the F/A-18's cue that a weapon can't fire.</summary>
        private sealed class Cross : VisualElement
        {
            private readonly float width;
            private Color colour = AxiomPalette.Text;

            public Cross(float width)
            {
                this.width = width;
                generateVisualContent = Draw;
            }

            public Color Colour
            {
                set
                {
                    if (value == colour) return;
                    colour = value;
                    MarkDirtyRepaint();
                }
            }

            private void Draw(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (w <= 0f || h <= 0f) return;
                Painter2D painter = context.painter2D;
                painter.lineWidth = width;
                painter.strokeColor = colour;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, 0f));
                painter.LineTo(new Vector2(w, h));
                painter.MoveTo(new Vector2(w, 0f));
                painter.LineTo(new Vector2(0f, h));
                painter.Stroke();
            }
        }
    }
}

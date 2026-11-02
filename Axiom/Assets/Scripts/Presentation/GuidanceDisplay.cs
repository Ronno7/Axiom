using GeometricFlight.Core;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// The system's one voice on the HUD, and its pointer. The panel at top centre holds one short centred line, whose
    /// controls ("[LEFT SHIFT]") draw as key chips: a lesson's standing instruction (<see cref="SetCard"/>), which a passing
    /// announcement from the HUD's slot covers until it ends. A line holds long enough to read, so quick state changes
    /// can't flicker. With nothing new to do it stays empty. A flag marks one object in the world: a short Cyan leader
    /// ending beside it and a tab with an action and a distance; off screen it points from the edge instead. It joins the
    /// HUD document's screen, so arcade mode carries it. It renders what it is told; lessons decide what and when.
    /// </summary>
    [DefaultExecutionOrder(110)] // After the camera has moved.
    [DisallowMultipleComponent]
    public sealed class GuidanceDisplay : MonoBehaviour
    {
        [Tooltip("The HUD document (LevelUi).")]
        [SerializeField] private UIDocument document;
        [Tooltip("Distances are measured from here (the ship); from the camera if empty.")]
        [SerializeField] private Transform distanceFrom;
        [Tooltip("Off-screen pointer inset from the left, top, right and bottom edges in panel pixels, clear of the CRT "
               + "curve and the HUD corners.")]
        [SerializeField] private Vector4 edgeInset = new Vector4(120f, 150f, 120f, 210f);
        [SerializeField, Min(0.05f)] private float ruleDrawSeconds = 0.3f;
        [Tooltip("A standing line holds at least this long before another replaces it. Announcements cut in at once.")]
        [SerializeField, Min(0f)] private float lineHoldSeconds = 1.2f;

        private const float LeaderGap = 10f, LeaderLength = 26f;

        private sealed class Line
        {
            public string Text;
            public bool Emphasis, Arrival;
            public bool Same(Line other) => other != null && Text == other.Text && Emphasis == other.Emphasis
                && Arrival == other.Arrival;
        }

        private VisualElement screen, addressLine, address, rule, lineRow, flag, leader, tab;
        private Label flagLabel;
        private Arrow arrow;
        private Camera view;
        private Transform objective;
        private string objectiveAction;
        private Line card, announcement, shown;
        private bool chipsLit;
        private float shownAt, lineAt;
        private LevelUi levelUi;

        /// <summary>Marks an object in the world with an action ("SHOOT") and its distance; no action, the distance alone.</summary>
        public void ShowObjective(Transform target, string action = null)
        {
            objective = target;
            objectiveAction = action;
        }

        public void ClearObjective() => objective = null;

        /// <summary>The standing instruction. Brackets mark controls, drawn as chips.</summary>
        /// <param name="arrival">A hostile arrival: Orange instead of Cyan, which the words must also say.</param>
        public void SetCard(string text, bool emphasis = false, bool arrival = false) =>
            card = new Line { Text = text, Emphasis = emphasis, Arrival = arrival };

        public void ClearCard() => card = null;

        private void SetAnnouncement(string text) => announcement = text == null ? null : new Line { Text = text };

        /// <summary>Fills the chips while their control is held: the input arrived, nothing more.</summary>
        public bool ChipsLit
        {
            set
            {
                if (value == chipsLit) return;
                chipsLit = value;
                if (lineRow == null) return;
                foreach (VisualElement child in lineRow.Children())
                    if (child is KeyChip chip) chip.Lit = value;
            }
        }

        private void OnEnable()
        {
            levelUi = document != null ? document.GetComponent<LevelUi>() : null;
            if (levelUi != null) levelUi.AddressSink = SetAnnouncement;
        }

        private void LateUpdate()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            VisualElement target = root.Q("screen") ?? root;
            if (address == null || addressLine.parent != target) Build(target);
            ShowAddress();
            PlaceFlag();
        }

        private void Build(VisualElement target)
        {
            addressLine?.RemoveFromHierarchy();
            flag?.RemoveFromHierarchy();
            screen = target;

            // A full-width line centres the panel at any length, leaving the corners to the time and targets.
            addressLine = new VisualElement { name = "guidance-address", pickingMode = PickingMode.Ignore };
            addressLine.style.position = Position.Absolute;
            addressLine.style.left = addressLine.style.right = 0f;
            addressLine.style.top = 34f;
            addressLine.style.alignItems = Align.Center;
            address = new VisualElement { pickingMode = PickingMode.Ignore };
            address.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.88f);
            address.style.paddingLeft = address.style.paddingRight = 26f;
            address.style.paddingTop = 10f;
            address.style.paddingBottom = 12f;
            addressLine.Add(address);
            rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.style.position = Position.Absolute;
            rule.style.top = 0f;
            rule.style.alignSelf = Align.Center;
            rule.style.height = 2f;
            address.Add(rule);
            lineRow = new VisualElement { pickingMode = PickingMode.Ignore };
            lineRow.style.flexDirection = FlexDirection.Row;
            lineRow.style.alignItems = Align.Center;
            lineRow.style.justifyContent = Justify.Center;
            address.Add(lineRow);
            screen.Add(addressLine);

            flag = new VisualElement { name = "guidance-flag", pickingMode = PickingMode.Ignore };
            flag.style.position = Position.Absolute;
            flag.style.left = flag.style.top = 0f;
            leader = new VisualElement { pickingMode = PickingMode.Ignore };
            leader.style.position = Position.Absolute;
            leader.style.width = 2f;
            leader.style.height = LeaderLength;
            leader.style.backgroundColor = AxiomPalette.Cyan;
            flag.Add(leader);
            tab = new VisualElement { pickingMode = PickingMode.Ignore };
            tab.style.position = Position.Absolute;
            tab.style.flexDirection = FlexDirection.Row;
            tab.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.8f);
            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.style.width = 4f;
            mark.style.backgroundColor = AxiomPalette.Cyan;
            tab.Add(mark);
            flagLabel = new Label { pickingMode = PickingMode.Ignore };
            flagLabel.style.fontSize = 17f;
            flagLabel.style.letterSpacing = 1f;
            flagLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            flagLabel.style.color = AxiomPalette.Light;
            flagLabel.style.paddingLeft = 7f;
            flagLabel.style.paddingRight = 8f;
            flagLabel.style.paddingTop = flagLabel.style.paddingBottom = 2f;
            tab.Add(flagLabel);
            flag.Add(tab);
            arrow = new Arrow();
            flag.Add(arrow);
            screen.Add(flag);
            shown = null;
        }

        /// <summary>An announcement covers the standing line until it ends. A standing line holds long enough to read
        /// before another replaces it or it clears. Behind the pause menu there is nothing to do, and the rule draws out only
        /// when the panel appears.</summary>
        private void ShowAddress()
        {
            bool paused = Time.timeScale <= 0f;
            Line line = paused ? null : announcement ?? card;
            if (!paused && announcement == null && shown != null && !shown.Same(line)
                && UiClock.Now - lineAt < lineHoldSeconds) line = shown;
            addressLine.style.display = line != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (line == null)
            {
                shown = null;
                return;
            }
            if (shown == null) shownAt = UiClock.Now;
            rule.style.width = Length.Percent(Mathf.Clamp01((UiClock.Now - shownAt) / ruleDrawSeconds) * 100f);
            if (line.Same(shown)) return;
            shown = line;
            lineAt = UiClock.Now;
            rule.style.backgroundColor = line.Arrival ? AxiomPalette.Orange : line.Emphasis ? AxiomPalette.Light : AxiomPalette.Cyan;
            FillLine(line);
        }

        private void FillLine(Line line)
        {
            lineRow.Clear();
            string text = line.Text;
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i), close = open < 0 ? -1 : text.IndexOf(']', open + 1);
                if (close < 0)
                {
                    AddWords(text.Substring(i), line.Arrival);
                    break;
                }
                AddWords(text.Substring(i, open - i), line.Arrival);
                lineRow.Add(new KeyChip(text.Substring(open + 1, close - open - 1)) { Lit = chipsLit });
                i = close + 1;
            }
        }

        private void AddWords(string words, bool arrival)
        {
            words = words.Trim();
            if (words.Length == 0) return;
            var label = new Label(words) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 30f;
            label.style.letterSpacing = 3f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = arrival ? AxiomPalette.Orange : AxiomPalette.Light;
            label.style.paddingLeft = label.style.paddingRight = 0f;
            label.style.paddingTop = label.style.paddingBottom = 0f;
            lineRow.Add(label);
        }

        private void PlaceFlag()
        {
            if (view == null) view = Camera.main;
            float w = screen.resolvedStyle.width, h = screen.resolvedStyle.height;
            bool show = objective != null && objective.gameObject.activeInHierarchy && view != null
                && Time.timeScale > 0f && w > 0f && h > 0f && !float.IsNaN(w) && !float.IsNaN(h);
            flag.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            Vector3 from = distanceFrom != null ? distanceFrom.position : view.transform.position;
            int metres = Mathf.RoundToInt(Vector3.Distance(from, objective.position));
            flagLabel.text = string.IsNullOrEmpty(objectiveAction) ? metres + " m" : objectiveAction + "  " + metres + " m";
            float tabW = Resolved(tab.resolvedStyle.width, 140f), tabH = Resolved(tab.resolvedStyle.height, 22f);

            Vector3 point = view.WorldToViewportPoint(objective.position);
            float x = point.x * w, y = (1f - point.y) * h;
            bool onScreen = point.z > 0f && x >= 0f && x <= w && y >= 0f && y <= h;
            leader.style.display = onScreen ? DisplayStyle.Flex : DisplayStyle.None;
            arrow.style.display = onScreen ? DisplayStyle.None : DisplayStyle.Flex;
            if (onScreen)
            {
                // The leader ends beside the object, never on it, and the tab sits on top of the leader.
                float lx = x + LeaderGap, top = y - LeaderGap - LeaderLength;
                leader.style.left = lx;
                leader.style.top = top;
                tab.style.left = Mathf.Min(lx, w - tabW - 8f);
                tab.style.top = Mathf.Max(8f, top - tabH);
                return;
            }

            // Off screen: point from the edge of a safe rectangle, toward the object (mirrored when behind).
            var centre = new Vector2(w * 0.5f, h * 0.5f);
            var direction = new Vector2(x, y) - centre;
            if (point.z < 0f) direction = -direction;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.up;
            float halfW = Mathf.Max(1f, centre.x - Mathf.Max(edgeInset.x, edgeInset.z));
            float halfH = Mathf.Max(1f, centre.y - Mathf.Max(edgeInset.y, edgeInset.w));
            float scale = Mathf.Min(halfW / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
                halfH / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            Vector2 edge = centre + direction * scale;
            Vector2 unit = direction.normalized;
            arrow.style.left = edge.x - Arrow.Size * 0.5f;
            arrow.style.top = edge.y - Arrow.Size * 0.5f;
            arrow.style.rotate = new Rotate(new Angle(Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg, AngleUnit.Degree));
            Vector2 tabCentre = edge - unit * (Arrow.Size + Mathf.Abs(unit.x) * tabW * 0.5f + Mathf.Abs(unit.y) * tabH * 0.5f + 4f);
            tab.style.left = tabCentre.x - tabW * 0.5f;
            tab.style.top = tabCentre.y - tabH * 0.5f;
        }

        private static float Resolved(float value, float fallback) => float.IsNaN(value) || value <= 0f ? fallback : value;

        private void OnDisable()
        {
            if (levelUi != null) levelUi.AddressSink = null;
            addressLine?.RemoveFromHierarchy();
            flag?.RemoveFromHierarchy();
            address = null;
            addressLine = null;
            flag = null;
            lineRow = null;
        }

        /// <summary>A Cyan pointer, drawn facing right and rotated toward the object.</summary>
        private sealed class Arrow : VisualElement
        {
            public const float Size = 14f;

            public Arrow()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.width = style.height = Size;
                generateVisualContent = Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                painter.fillColor = AxiomPalette.Cyan;
                painter.BeginPath();
                painter.MoveTo(new Vector2(2f, 1f));
                painter.LineTo(new Vector2(Size - 1f, Size * 0.5f));
                painter.LineTo(new Vector2(2f, Size - 1f));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>A control name in a forward-leaning key box, in the menu buttons' lean. Filled while the control is
    /// held: the input arrived, which is not the same as the action succeeding.</summary>
    public sealed class KeyChip : Label
    {
        private const float Lean = 6f, Stroke = 1.5f;
        private bool lit;

        public KeyChip(string key) : base(key)
        {
            pickingMode = PickingMode.Ignore;
            style.fontSize = 24f;
            style.letterSpacing = 1f;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.paddingLeft = style.paddingRight = 10f;
            style.paddingTop = style.paddingBottom = 1f;
            style.marginLeft = style.marginRight = 6f;
            // Draw the box first so the label's own text renders on top of it.
            System.Action<MeshGenerationContext> text = generateVisualContent;
            generateVisualContent = Draw;
            if (text != null) generateVisualContent += text;
            Lit = false;
        }

        public bool Lit
        {
            get => lit;
            set
            {
                lit = value;
                style.color = value ? AxiomPalette.Void : AxiomPalette.Light;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            float i = Stroke * 0.5f;
            var corners = new[] { new Vector2(Lean + i, i), new Vector2(w - i, i), new Vector2(w - Lean - i, h - i), new Vector2(i, h - i) };
            if (lit)
            {
                MeshWriteData mesh = context.Allocate(4, 6);
                foreach (Vector2 corner in corners)
                    mesh.SetNextVertex(new Vertex { position = new Vector3(corner.x, corner.y, Vertex.nearZ), tint = AxiomPalette.Light });
                mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
                mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
            }
            Painter2D painter = context.painter2D;
            painter.lineWidth = Stroke;
            painter.lineJoin = LineJoin.Miter;
            painter.strokeColor = AxiomPalette.Cyan;
            painter.BeginPath();
            painter.MoveTo(corners[0]);
            for (int c = 1; c < corners.Length; c++) painter.LineTo(corners[c]);
            painter.ClosePath();
            painter.Stroke();
        }
    }
}

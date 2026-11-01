using System;
using System.Collections.Generic;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>
    /// The entry's central systems readout, top centre, built in code inside an existing document so the
    /// original menu layout stays untouched. Sampled from the entry clock (no timers), so pause and replay
    /// are exact. Rows print on their beats, scroll at six and emphasise only the newest; live rows show
    /// current state through <c>live(key)</c>. At the end the rows retire and the panel folds upward.
    /// </summary>
    public sealed class EntryTerminal
    {
        private sealed class Row
        {
            public VisualElement Element, Marker;
            public Label Key, Value;
        }

        private readonly VisualElement container, frame, list, header, headerRule, columnRule;
        private readonly List<Row> rows = new List<Row>();

        /// <summary>Rows on screen after the last Sample, and the newest row's current text.</summary>
        public int VisibleRows { get; private set; }
        public string NewestText { get; private set; } = string.Empty;

        public EntryTerminal(VisualElement parent)
        {
            container = new VisualElement { name = "entry-systems", pickingMode = PickingMode.Ignore };
            container.style.position = Position.Absolute;
            container.style.left = Length.Percent(50f);
            container.style.translate = new Translate(Length.Percent(-50f), 0f);
            container.style.top = Length.Percent(8f);
            container.style.width = Length.Percent(36f);

            frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.style.paddingLeft = frame.style.paddingRight = 18f;
            frame.style.paddingTop = 8f;
            frame.style.paddingBottom = 10f;
            // A soft backing keeps rows legible over bright ribs without hiding the world.
            frame.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.55f);
            Color edge = AxiomPalette.WithAlpha(AxiomPalette.Edge, 0.8f);
            frame.style.borderTopColor = frame.style.borderBottomColor = edge;
            frame.style.borderLeftColor = frame.style.borderRightColor = edge;
            frame.style.borderTopWidth = frame.style.borderBottomWidth = 1f;
            frame.style.borderLeftWidth = frame.style.borderRightWidth = 1f;
            frame.style.transformOrigin = new TransformOrigin(Length.Percent(50f), 0f);

            header = new Label("SYSTEMS") { pickingMode = PickingMode.Ignore };
            header.style.fontSize = 15f;
            header.style.letterSpacing = 6f;
            header.style.color = AxiomPalette.Muted;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.style.marginBottom = 2f;
            headerRule = new VisualElement { pickingMode = PickingMode.Ignore };
            headerRule.style.height = 1f;
            headerRule.style.marginBottom = 6f;
            headerRule.style.backgroundColor = AxiomPalette.Edge;
            headerRule.style.transformOrigin = new TransformOrigin(Length.Percent(50f), 0f);
            frame.Add(header);
            frame.Add(headerRule);

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            list = new VisualElement { pickingMode = PickingMode.Ignore };
            columnRule = new VisualElement { pickingMode = PickingMode.Ignore };
            columnRule.style.position = Position.Absolute;
            columnRule.style.left = 200f;
            columnRule.style.top = 2f;
            columnRule.style.bottom = 2f;
            columnRule.style.width = 1f;
            columnRule.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Edge, 0.6f);
            body.Add(columnRule);
            body.Add(list);
            frame.Add(body);
            foreach (TerminalLine line in EntrySequence.Script) rows.Add(MakeRow(line));
            container.Add(frame);
            container.style.display = DisplayStyle.None;
            parent.Add(container);
        }

        public void Hide() => container.style.display = DisplayStyle.None;

        public void Remove() => container.RemoveFromHierarchy();

        public void Sample(float elapsed, float tempo, Func<string, string> live)
        {
            float beatLength = EntrySequence.BeatLength(tempo);
            float beat = elapsed / beatLength;
            VisibleRows = 0;
            if (elapsed < 0f || beat < EntrySequence.TerminalsBeat || beat >= EntrySequence.Beats)
            {
                Hide();
                return;
            }
            container.style.display = DisplayStyle.Flex;

            // The panel opens from its centre line: the header rule draws outward, then the frame drops.
            float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beat - EntrySequence.TerminalsBeat) / 0.5f));
            headerRule.style.scale = new Scale(new Vector3(open, 1f, 1f));
            float collapse = Mathf.Clamp01((beat - EntrySequence.CollapseBeat) / 1f);
            frame.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.02f, open * (1f - 0.98f * collapse)), 1f));
            frame.style.opacity = 1f - collapse;
            columnRule.style.opacity = open;

            int printed = 0;
            for (int i = 0; i < rows.Count; i++) if (beat >= EntrySequence.Script[i].Beat) printed = i + 1;
            int retired = beat < EntrySequence.RetireBeat ? 0
                : Mathf.FloorToInt((beat - EntrySequence.RetireBeat) / 0.2f) + 1;
            int first = Mathf.Max(0, printed - EntrySequence.VisibleRows);

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool shown = i < printed && i >= first + Mathf.Min(retired, EntrySequence.VisibleRows);
                row.Element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;

                TerminalLine line = EntrySequence.Script[i];
                string value = line.Value ?? live?.Invoke(line.LiveKey) ?? string.Empty;
                int typed = Mathf.FloorToInt((elapsed - line.Beat * beatLength) * EntrySequence.CharactersPerSecond);
                row.Key.text = Prefix(line.Key, typed);
                bool typing = typed < line.Key.Length + value.Length;
                row.Value.text = Prefix(value, typed - line.Key.Length) + (typing ? "_" : string.Empty);
                VisibleRows++;
                if (i == printed - 1) NewestText = row.Key.text + " " + row.Value.text;
                // Only the newest row is emphasised, and only briefly.
                bool newest = i == printed - 1 && beat - line.Beat < 1.5f;
                row.Value.style.color = newest ? AxiomPalette.Light : AxiomPalette.Text;
            }
        }

        private static string Prefix(string text, int count) =>
            count <= 0 ? string.Empty : count >= text.Length ? text : text.Substring(0, count);

        private Row MakeRow(TerminalLine line)
        {
            var row = new Row { Element = new VisualElement { pickingMode = PickingMode.Ignore } };
            row.Element.style.flexDirection = FlexDirection.Row;
            row.Element.style.alignItems = Align.Center;
            row.Element.style.height = 28f;
            // A small bar marks who is speaking: cyan for the craft's systems, orange for the Grid.
            row.Marker = Line(3f, 13f, line.Craft ? AxiomPalette.Cyan : AxiomPalette.Orange);
            row.Marker.style.marginRight = 12f;
            row.Key = Text(AxiomPalette.Muted, 170f);
            row.Value = Text(AxiomPalette.Text, -1f);
            row.Value.style.paddingLeft = 16f;
            row.Element.Add(row.Marker);
            row.Element.Add(row.Key);
            row.Element.Add(row.Value);
            row.Element.style.display = DisplayStyle.None;
            list.Add(row.Element);
            return row;
        }

        private static Label Text(Color color, float width)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 18f;
            label.style.letterSpacing = 2f;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = 0f;
            label.style.paddingLeft = label.style.paddingRight = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            if (width > 0f) label.style.width = width;
            else label.style.flexGrow = 1f;
            return label;
        }

        private static VisualElement Line(float width, float height, Color color)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.width = width;
            if (height > 0f) element.style.height = height;
            element.style.backgroundColor = color;
            return element;
        }
    }
}

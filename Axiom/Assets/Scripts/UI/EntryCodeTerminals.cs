using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>
    /// Short-lived code windows at the screen edges during the entry. Each streams startup pseudo-code for
    /// what is actually happening (links, the passage build, the craft, the flight model) in readable bursts
    /// that pause on results, then folds shut. Only one streams at a time. Values in {braces} come from
    /// <c>live(key)</c> when the line prints. Sampled from the entry clock, so pause and replay are exact.
    /// </summary>
    public sealed class EntryCodeTerminals
    {
        /// <summary>One window: where it sits, when it opens, streams and closes, and what it prints.
        /// Lines starting with "&gt;" are commands, "»" results (both brighter); a trailing "|n" holds n beats.</summary>
        public sealed class Window
        {
            public string Title;
            public bool Craft, Right;
            public float TopPercent, OpenBeat, StreamBeat, CloseBeat;
            public string[] Lines;
        }

        public const float CharactersPerSecond = 320f;
        public const int MaxLines = 9;

        public static readonly Window[] Script =
        {
            new Window { Title = "grid://link", Right = false, TopPercent = 24f, OpenBeat = 6.8f, StreamBeat = 7f, CloseBeat = 9.1f,
                Lines = new[] {
                    "> open link", "  scan.devices()", "    keyboard ........ ready", "    cockpit ......... {cockpit}",
                    "  route.input = {input}", "» input routed|0.4",
                    "> load preferences", "  handling = {handling}", "  invert   = {invert}", "  display  = {display}",
                    "» preferences loaded|0.3" } },
            new Window { Title = "grid://passage.rez", Right = true, TopPercent = 24f, OpenBeat = EntrySequence.TerminalsBeat, StreamBeat = 4.2f, CloseBeat = 7.2f,
                Lines = new[] {
                    "> rez passage --w {width} --h {height} --len {length}", "  alloc sector(z0 = {mouth}) ok",
                    "  seams.extend(from = mouth)", "  for i in 0..{ribs}:", "      rib[i].draw(feet -> apex)",
                    "    rib[00] z={rib:0}  ok", "    rib[01] z={rib:1}  ok", "    rib[02] z={rib:2}  ok",
                    "    rib[03] z={rib:3}  ok", "    rib[04] z={rib:4}  ok", "» ribs drawing ahead|0.2",
                    "  walls.clad(left, right)", "  canopy.seal()", "» cladding ahead of craft|0.5" } },
            new Window { Title = "craft://compile", Craft = true, Right = false, TopPercent = 48f, OpenBeat = 8.9f, StreamBeat = 9.1f, CloseBeat = 11.6f,
                Lines = new[] {
                    "> compile craft", "  hull.mesh ......... load", "  hull.material ..... construct",
                    "  shield.segments = {shieldMax}", "  shield.state    = {shield}", "  arm.switch      = {arm}",
                    "  weapons.release = after-release", "  hud.bind(throttle, speed, targets)", "» craft rendering|1" } },
            new Window { Title = "craft://flight", Craft = true, Right = true, TopPercent = 48f, OpenBeat = 11.6f, StreamBeat = 11.8f, CloseBeat = 14.4f,
                Lines = new[] {
                    "> init flight", "  model    = nose_led", "  limit    = {limit} deg", "  handling = {handling}",
                    "  speed    = {min}-{max} m/s, cruise {cruise}", "  throttle = {throttle}", "  boost    = {boost}",
                    "» control ready for hand-over|1" } },
        };

        private static readonly Regex Token = new Regex(@"\{([a-zA-Z]+(?::\d+)?)\}");

        private sealed class View
        {
            public Window Source;
            public VisualElement Root, Frame, Body;
            public Label[] Rows;
            public float[] Starts;
            public string[] Text;
        }

        private readonly List<View> views = new List<View>();

        /// <summary>Windows on screen and streaming after the last Sample.</summary>
        public int OpenWindows { get; private set; }
        public int StreamingWindows { get; private set; }

        public EntryCodeTerminals(VisualElement parent, Font font)
        {
            foreach (Window window in Script) views.Add(Build(parent, window, font));
        }

        public void Hide()
        {
            foreach (View view in views) view.Root.style.display = DisplayStyle.None;
            OpenWindows = StreamingWindows = 0;
        }

        public void Remove()
        {
            foreach (View view in views) view.Root.RemoveFromHierarchy();
        }

        /// <summary>When each line starts typing (beats), from line lengths, typing speed and holds.</summary>
        public static float[] LineStarts(Window window, float tempo, Func<string, string> live)
        {
            float beatLength = EntrySequence.BeatLength(tempo);
            var starts = new float[window.Lines.Length];
            float at = window.StreamBeat;
            for (int i = 0; i < window.Lines.Length; i++)
            {
                starts[i] = at;
                string text = Resolve(Body(window.Lines[i], out float hold), live);
                at += text.Length / CharactersPerSecond / beatLength + hold;
            }
            return starts;
        }

        public void Sample(float elapsed, float tempo, Func<string, string> live)
        {
            float beatLength = EntrySequence.BeatLength(tempo);
            float beat = elapsed / beatLength;
            OpenWindows = StreamingWindows = 0;
            foreach (View view in views)
            {
                Window w = view.Source;
                bool open = elapsed >= 0f && beat >= w.OpenBeat && beat < w.CloseBeat;
                view.Root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                if (!open) continue;
                OpenWindows++;
                view.Starts ??= LineStarts(w, tempo, live);

                // Frame draws open over a fifth of a beat and folds shut over the last fifth.
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beat - w.OpenBeat) / 0.2f));
                float shut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((w.CloseBeat - beat) / 0.2f));
                view.Frame.style.scale = new Scale(new Vector3(grow, Mathf.Max(0.02f, shut), 1f));

                int printed = 0;
                bool streaming = false;
                for (int i = 0; i < w.Lines.Length; i++)
                {
                    if (beat < view.Starts[i]) break;
                    printed = i + 1;
                    view.Text[i] ??= Resolve(Body(w.Lines[i], out _), live);
                    int typed = Mathf.FloorToInt((beat - view.Starts[i]) * beatLength * CharactersPerSecond);
                    if (typed < view.Text[i].Length) streaming = true;
                }
                if (streaming) StreamingWindows++;

                int first = Mathf.Max(0, printed - MaxLines);
                for (int r = 0; r < MaxLines; r++)
                {
                    int i = first + r;
                    Label row = view.Rows[r];
                    if (i >= printed) { row.text = string.Empty; continue; }
                    string text = view.Text[i];
                    int typed = Mathf.FloorToInt((beat - view.Starts[i]) * beatLength * CharactersPerSecond);
                    bool typing = typed < text.Length;
                    row.text = (typing ? text.Substring(0, Mathf.Max(0, typed)) + "_" : text);
                    row.style.color = text.StartsWith("»") ? AxiomPalette.Light
                        : text.StartsWith(">") ? AxiomPalette.Text : AxiomPalette.Muted;
                }
            }
        }

        private static string Body(string line, out float hold)
        {
            hold = 0f;
            int bar = line.LastIndexOf('|');
            if (bar < 0) return line;
            float.TryParse(line.Substring(bar + 1), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out hold);
            return line.Substring(0, bar);
        }

        private static string Resolve(string text, Func<string, string> live) =>
            Token.Replace(text, m => live?.Invoke(m.Groups[1].Value) ?? m.Value);

        private static View Build(VisualElement parent, Window w, Font font)
        {
            var view = new View { Source = w, Rows = new Label[MaxLines], Text = new string[w.Lines.Length] };
            view.Root = new VisualElement { name = "entry-code " + w.Title, pickingMode = PickingMode.Ignore };
            view.Root.style.position = Position.Absolute;
            view.Root.style.top = Length.Percent(w.TopPercent);
            if (w.Right) view.Root.style.right = Length.Percent(3f); else view.Root.style.left = Length.Percent(3f);
            view.Root.style.width = Length.Percent(27f);
            view.Root.style.display = DisplayStyle.None;

            view.Frame = new VisualElement { pickingMode = PickingMode.Ignore };
            view.Frame.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.72f);
            Color edge = AxiomPalette.WithAlpha(AxiomPalette.Edge, 0.9f);
            view.Frame.style.borderTopColor = view.Frame.style.borderBottomColor = edge;
            view.Frame.style.borderLeftColor = view.Frame.style.borderRightColor = edge;
            view.Frame.style.borderTopWidth = view.Frame.style.borderBottomWidth = 1f;
            view.Frame.style.borderLeftWidth = view.Frame.style.borderRightWidth = 1f;
            view.Frame.style.paddingLeft = view.Frame.style.paddingRight = 10f;
            view.Frame.style.paddingTop = 4f;
            view.Frame.style.paddingBottom = 6f;
            view.Frame.style.transformOrigin = new TransformOrigin(Length.Percent(w.Right ? 100f : 0f), 0f);

            var title = new Label(w.Title) { pickingMode = PickingMode.Ignore };
            Style(title, font, 14f, w.Craft ? AxiomPalette.Cyan : AxiomPalette.Orange);
            title.style.borderBottomColor = edge;
            title.style.borderBottomWidth = 1f;
            title.style.marginBottom = 4f;
            title.style.paddingBottom = 2f;
            view.Frame.Add(title);

            view.Body = new VisualElement { pickingMode = PickingMode.Ignore };
            for (int r = 0; r < MaxLines; r++)
            {
                var row = new Label { pickingMode = PickingMode.Ignore };
                Style(row, font, 15f, AxiomPalette.Muted);
                row.style.height = 20f;
                row.style.whiteSpace = WhiteSpace.NoWrap;
                row.style.overflow = Overflow.Hidden;
                view.Rows[r] = row;
                view.Body.Add(row);
            }
            view.Frame.Add(view.Body);
            view.Root.Add(view.Frame);
            parent.Add(view.Root);
            return view;
        }

        private static void Style(Label label, Font font, float size, Color color)
        {
            if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0f;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
        }
    }
}

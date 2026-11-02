using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>
    /// Draws one HUD readout into place during the entry, the way the throttle gauge assembles: a thin rule traces
    /// out under the readout, then its live text types in behind a cursor, then the rule settles away. The text is
    /// always the real value, never a count-up, and the readout keeps its full width while typing so nothing
    /// shifts. At 1 the readout is exactly its normal self.
    /// </summary>
    public sealed class InstrumentBuild
    {
        public const string RuleClass = "instrument-build-rule";
        /// <summary>Typing runs over this part of the assembly; the rule draws before it.</summary>
        public const float TypeFrom = 0.3f, TypeTo = 0.85f;

        private readonly Label label;
        private readonly VisualElement rule;
        private string text = string.Empty;
        private float amount = 1f;

        /// <summary>The rule is a sibling under the label (a label with children stops sizing to its text).</summary>
        public InstrumentBuild(Label label, bool fromRight)
        {
            this.label = label;
            text = label.text ?? string.Empty;
            rule = new VisualElement { name = label.name + "-build", pickingMode = PickingMode.Ignore };
            rule.AddToClassList(RuleClass);
            rule.style.transformOrigin = new TransformOrigin(Length.Percent(fromRight ? 100f : 0f), Length.Percent(50f));
            rule.style.display = DisplayStyle.None;
            label.parent?.Insert(label.parent.IndexOf(label) + 1, rule);
            label.RegisterCallback<GeometryChangedEvent>(_ => PlaceRule());
        }

        public float Amount => amount;
        public VisualElement Rule => rule;

        /// <summary>The readout's real text, shown in full once built.</summary>
        public string Text
        {
            get => text;
            set
            {
                value ??= string.Empty;
                if (value == text) return;
                text = value;
                Apply();
            }
        }

        public void SetAmount(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, amount) && value < 1f) return;
            amount = value;
            Apply();
        }

        /// <summary>An inline opacity only while partial, so class-driven looks resume afterward.</summary>
        public static StyleFloat Partial(float amount) =>
            amount >= 1f ? new StyleFloat(StyleKeyword.Null) : new StyleFloat(amount);

        /// <summary>The first part of <paramref name="text"/> typed behind a cursor; <paramref name="typed"/>
        /// runs 0–1.</summary>
        public static string Reveal(string text, float typed)
        {
            if (string.IsNullOrEmpty(text) || typed >= 1f) return text ?? string.Empty;
            if (typed <= 0f) return string.Empty;
            int count = Mathf.Clamp(Mathf.FloorToInt(text.Length * typed), 0, text.Length - 1);
            return text.Substring(0, count) + "_";
        }

        private void Apply()
        {
            if (amount >= 1f || Hidden)
            {
                rule.style.display = DisplayStyle.None;
                label.style.minWidth = StyleKeyword.Null;
                label.text = text;
                return;
            }
            // Hold the finished width so a right-anchored readout types in place.
            Vector2 size = label.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f,
                VisualElement.MeasureMode.Undefined);
            if (float.IsFinite(size.x) && size.x > 0f)
                label.style.minWidth = size.x + label.resolvedStyle.paddingLeft + label.resolvedStyle.paddingRight;
            rule.style.display = DisplayStyle.Flex;
            rule.style.scale = new Scale(new Vector3(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.35f, amount)), 1f, 1f));
            rule.style.opacity = 1f - Mathf.InverseLerp(0.8f, 1f, amount);
            label.text = Reveal(text, Mathf.InverseLerp(TypeFrom, TypeTo, amount));
            PlaceRule();
        }

        // A readout switched off (inline or by its stylesheet) draws no rule either.
        private bool Hidden => label.style.display.keyword == StyleKeyword.Undefined
            ? label.style.display.value == DisplayStyle.None
            : label.resolvedStyle.display == DisplayStyle.None;

        private void PlaceRule()
        {
            Rect r = label.layout;
            if (float.IsNaN(r.width)) return;
            rule.style.left = r.x;
            rule.style.top = r.yMax + 4f;
            rule.style.width = r.width;
        }
    }
}

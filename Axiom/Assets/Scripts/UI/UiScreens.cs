using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Small helpers shared by the menu and level UI.</summary>
    public static class UiScreens
    {
        /// <summary>Shows one panel (or none) and hides the rest. Focus moves to
        /// <paramref name="focus"/> on the next UI update, once the panel is laid out.</summary>
        public static void ShowOnly(VisualElement shown, VisualElement focus, params VisualElement[] panels)
        {
            foreach (VisualElement panel in panels)
                panel.style.display = panel == shown ? DisplayStyle.Flex : DisplayStyle.None;
            focus?.schedule.Execute(focus.Focus);
        }

        /// <summary>Makes focus visible on composite fields (sliders, toggles), whose inner input
        /// element receives focus rather than the field itself.</summary>
        public static void MarkFocusWithin(VisualElement field, string className = "option--focused")
        {
            field.RegisterCallback<FocusInEvent>(_ => field.AddToClassList(className));
            field.RegisterCallback<FocusOutEvent>(_ => field.RemoveFromClassList(className));
        }

        /// <summary>The focused control's caption in capitals (button text or field label), or null.
        /// Sliders and toggles focus an inner element, so this walks up to the field.</summary>
        public static string FocusedLabel(VisualElement root)
        {
            for (var e = root.panel?.focusController?.focusedElement as VisualElement; e != null; e = e.parent)
            {
                string label = e is Button b ? b.text : e is Toggle t ? t.label : e is Slider s ? s.label : null;
                if (label != null) return label.ToUpperInvariant();
            }
            return null;
        }

        /// <summary>Quits a desktop build. In the editor this exits Play Mode instead.</summary>
        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>m:ss.t, truncated to tenths.</summary>
        public static string FormatTime(float seconds)
        {
            int tenths = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 10f);
            return $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
        }
    }
}

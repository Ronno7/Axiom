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

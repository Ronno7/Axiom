using System;
using GeometricFlight.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Binds an OptionsPanel.uxml instance to GameSettings. Changes apply immediately
    /// and are saved when the panel closes.</summary>
    public sealed class OptionsPanel
    {
        public VisualElement Root { get; }
        public VisualElement FirstFocus => sensitivity;

        private readonly Slider sensitivity;
        private readonly Toggle invertVertical;
        private readonly Toggle buzzer;
        private readonly Toggle fan;
        private readonly Action back;

        public OptionsPanel(VisualElement root, Action back, Action calibrate)
        {
            Root = root;
            sensitivity = root.Q<Slider>("steering-sensitivity");
            invertVertical = root.Q<Toggle>("invert-vertical");
            buzzer = root.Q<Toggle>("controller-buzzer");
            fan = root.Q<Toggle>("controller-fan");

            sensitivity.lowValue = GameSettings.MinSteeringSensitivity;
            sensitivity.highValue = GameSettings.MaxSteeringSensitivity;
            sensitivity.SetValueWithoutNotify(GameSettings.SteeringSensitivity);
            ShowSensitivity(GameSettings.SteeringSensitivity);
            sensitivity.RegisterValueChangedCallback(e =>
            {
                // Snap to 5% steps so keyboard adjustment lands on round values.
                float snapped = Mathf.Round(e.newValue * 20f) / 20f;
                GameSettings.SteeringSensitivity = snapped;
                sensitivity.SetValueWithoutNotify(snapped);
                ShowSensitivity(snapped);
            });

            invertVertical.SetValueWithoutNotify(GameSettings.InvertVertical);
            invertVertical.RegisterValueChangedCallback(e => GameSettings.InvertVertical = e.newValue);
            buzzer.SetValueWithoutNotify(GameSettings.BuzzerEnabled);
            buzzer.RegisterValueChangedCallback(e => GameSettings.BuzzerEnabled = e.newValue);
            fan.SetValueWithoutNotify(GameSettings.FanEnabled);
            fan.RegisterValueChangedCallback(e => GameSettings.FanEnabled = e.newValue);

            UiScreens.MarkFocusWithin(sensitivity);
            UiScreens.MarkFocusWithin(invertVertical);
            UiScreens.MarkFocusWithin(buzzer);
            UiScreens.MarkFocusWithin(fan);
            this.back = back;
            root.Q<Button>("options-calibrate").clicked += calibrate;
            root.Q<Button>("options-back").clicked += Close;
        }

        /// <summary>Saves and returns to the previous screen (Back button or Esc).</summary>
        public void Close()
        {
            GameSettings.Save();
            back();
        }

        private void ShowSensitivity(float value) =>
            sensitivity.label = $"Steering sensitivity  {Mathf.RoundToInt(value * 100f)}%";
    }
}

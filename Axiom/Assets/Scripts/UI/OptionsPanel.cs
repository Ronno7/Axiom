using System;
using System.Collections.Generic;
using GeometricFlight.Core;
using GeometricFlight.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Binds an OptionsPanel.uxml instance to GameSettings. Changes apply immediately and are saved when the
    /// panel closes. The title and pause menus each have a panel, so call <see cref="Refresh"/> when showing one.</summary>
    public sealed class OptionsPanel
    {
        public VisualElement Root { get; }
        public VisualElement FirstFocus => sensitivity;

        private readonly Slider sensitivity;
        private readonly Action back;
        private readonly List<Action> refreshes = new List<Action>();

        public OptionsPanel(VisualElement root, Action back, Action calibrate)
        {
            Root = root;
            this.back = back;

            sensitivity = root.Q<Slider>("steering-sensitivity");
            sensitivity.lowValue = GameSettings.MinSteeringSensitivity;
            sensitivity.highValue = GameSettings.MaxSteeringSensitivity;
            sensitivity.RegisterValueChangedCallback(e =>
            {
                // Snap to 5% steps so keyboard adjustment lands on round values.
                float snapped = Mathf.Round(e.newValue * 20f) / 20f;
                GameSettings.SteeringSensitivity = snapped;
                sensitivity.SetValueWithoutNotify(snapped);
                ShowSensitivity(snapped);
            });
            UiScreens.MarkFocusWithin(sensitivity);
            refreshes.Add(() =>
            {
                sensitivity.SetValueWithoutNotify(GameSettings.SteeringSensitivity);
                ShowSensitivity(GameSettings.SteeringSensitivity);
            });

            Bind("invert-vertical", () => GameSettings.InvertVertical, v => GameSettings.InvertVertical = v);
            Bind("cockpit-controller", () => GameSettings.CockpitEnabled, v =>
            {
                GameSettings.CockpitEnabled = v;
                ControllerService.Instance.RefreshLink();
            });
            Bind("keyboard-throttle", () => GameSettings.KeyboardThrottleWithController,
                v => GameSettings.KeyboardThrottleWithController = v);
            Bind("controller-buzzer", () => GameSettings.BuzzerEnabled, v => GameSettings.BuzzerEnabled = v);
            Bind("arcade-mode", () => GameSettings.ArcadeMode, v => GameSettings.ArcadeMode = v);
            Bind("reduced-entry", () => GameSettings.ReducedEntryAnimation, v => GameSettings.ReducedEntryAnimation = v);
            Refresh();

            root.Q<Button>("options-calibrate").clicked += calibrate;
            root.Q<Button>("options-back").clicked += Close;
        }

        /// <summary>Shows the current settings.</summary>
        public void Refresh()
        {
            foreach (Action refresh in refreshes) refresh();
        }

        /// <summary>Saves and returns to the previous screen (Back button or Esc).</summary>
        public void Close()
        {
            GameSettings.Save();
            back();
        }

        private void Bind(string name, Func<bool> get, Action<bool> set)
        {
            Toggle toggle = Root.Q<Toggle>(name);
            toggle.RegisterValueChangedCallback(e => set(e.newValue));
            UiScreens.MarkFocusWithin(toggle);
            refreshes.Add(() => toggle.SetValueWithoutNotify(get()));
        }

        private void ShowSensitivity(float value) =>
            sensitivity.label = $"Steering sensitivity  {Mathf.RoundToInt(value * 100f)}%";
    }
}

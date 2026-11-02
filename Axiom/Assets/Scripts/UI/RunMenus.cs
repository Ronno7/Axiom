using GeometricFlight.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>The menus over a run: pause with Options, Help and calibration, and the results. Observes LevelFlow and
    /// calls its public actions; owns no rules.</summary>
    public sealed class RunMenus
    {
        private readonly LevelFlow flow;
        private readonly VisualElement screen, hud, pausePanel, resultsPanel, helpPanel, parRow;
        private readonly Button resumeButton, nextButton, retryButton, resultsMenuButton, helpBack;
        private readonly Label resultsTitle;
        private readonly DamageFeedback damageFeedback;
        private readonly OptionsPanel options;
        private readonly CalibrationPanel calibration;
        private readonly float failedResultsDelay, completedResultsDelay;
        private VisualElement shownPanel;

        public RunMenus(VisualElement root, LevelFlow flow, DamageFeedback damageFeedback, float failedResultsDelay,
            float completedResultsDelay)
        {
            this.flow = flow;
            this.damageFeedback = damageFeedback;
            this.failedResultsDelay = failedResultsDelay;
            this.completedResultsDelay = completedResultsDelay;
            screen = root.Q("screen");
            hud = root.Q("hud");
            pausePanel = root.Q("pause-panel");
            resultsPanel = root.Q("results-panel");
            helpPanel = root.Q("help-panel");
            parRow = root.Q("par-row");
            resultsTitle = root.Q<Label>("results-title");
            options = new OptionsPanel(root.Q("options-panel"), ShowPause, ShowCalibration);
            calibration = new CalibrationPanel(root.Q("calibration-panel"), ShowOptions);
            resumeButton = root.Q<Button>("resume-button");
            nextButton = root.Q<Button>("next-button");
            retryButton = root.Q<Button>("retry-button");
            helpBack = root.Q<Button>("help-back");
            resultsMenuButton = root.Q<Button>("results-menu-button");

            resumeButton.clicked += flow.Resume;
            root.Q<Button>("pause-retry-button").clicked += flow.Retry;
            root.Q<Button>("pause-options-button").clicked += ShowOptions;
            root.Q<Button>("pause-help-button").clicked += () => ShowOverlay(helpPanel, helpBack);
            root.Q<Button>("pause-menu-button").clicked += flow.ExitToMenu;
            helpBack.clicked += ShowPause;
            nextButton.clicked += flow.Continue;
            retryButton.clicked += flow.Retry;
            resultsMenuButton.clicked += flow.ExitToMenu;

            flow.StateChanged += OnStateChanged;
            flow.LeaveSubmenu = LeaveSubmenu;
            ShowOverlay(null, null);
        }

        public void Detach()
        {
            flow.StateChanged -= OnStateChanged;
            flow.LeaveSubmenu = null;
        }

        /// <summary>Calibration replaces the cockpit on the LCD while open.</summary>
        public void TickCalibration() => calibration.Tick();

        private void ShowOptions()
        {
            options.Refresh();
            ShowOverlay(options.Root, options.FirstFocus);
        }

        private void ShowCalibration()
        {
            calibration.Open();
            ShowOverlay(calibration.Root, calibration.FirstFocus);
        }

        private void OnStateChanged(RunState state)
        {
            switch (state)
            {
                case RunState.Playing:
                    // Resuming mid-calibration abandons it and restores the old values.
                    if (calibration.IsOpen) calibration.Cancel();
                    ShowOverlay(null, null);
                    break;
                case RunState.Paused: ShowPause(); break;
                case RunState.Completed:
                    screen.schedule.Execute(ShowResults).ExecuteLater((long)(completedResultsDelay * 1000f));
                    break;
                case RunState.Failed:
                    screen.schedule.Execute(ShowResults).ExecuteLater((long)(failedResultsDelay * 1000f));
                    break;
            }
        }

        private void ShowPause() => ShowOverlay(pausePanel, resumeButton);

        /// <summary>The pause input steps back out of a submenu, as Esc does on the title screen.</summary>
        private bool LeaveSubmenu()
        {
            if (calibration.IsOpen) calibration.Cancel();
            else if (shownPanel == options.Root) options.Close();
            else if (shownPanel == helpPanel) ShowPause();
            else return false;
            return true;
        }

        private void ShowResults()
        {
            // The numbers captured when the run ended (the cockpit LCD shows the same ones).
            RunResults results = flow.Results;
            bool completed = results.Completed;
            resultsTitle.text = completed ? flow.CompletedTitle : "SHIP DESTROYED";
            SetStat("result-time", UiScreens.FormatTime(results.Time));
            SetStat("result-targets", $"{results.TargetsDestroyed} / {results.TargetsTotal}");
            SetStat("result-accuracy", results.ShotsFired == 0 ? "-" : $"{Mathf.RoundToInt(results.Accuracy * 100f)}%");
            SetStat("result-shield", $"{results.ShieldRemaining} / {results.ShieldMax}");
            bool showPar = completed && flow.HasPar;
            parRow.style.display = showPar ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPar)
                SetStat("result-par", UiScreens.FormatTime(flow.ParTime)
                    + (results.Time <= flow.ParTime ? "  BEATEN" : "  MISSED"));
            bool canContinue = completed && flow.HasNextLevel;
            nextButton.style.display = canContinue ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = DisplayStyle.None;
            // The last completed level (or a finished tutorial) offers the menu first, not a replay.
            ShowOverlay(resultsPanel, canContinue ? nextButton : completed ? resultsMenuButton : retryButton);
        }

        private void SetStat(string name, string value) => resultsPanel.Q<Label>(name).text = value;

        private void ShowOverlay(VisualElement panel, VisualElement focus)
        {
            if (panel != null) damageFeedback.ResetPulse();
            shownPanel = panel;
            screen.EnableInClassList("screen--dimmed", panel != null);
            UiScreens.ShowOnly(panel, focus, pausePanel, resultsPanel, options.Root, helpPanel, calibration.Root);
        }
    }
}

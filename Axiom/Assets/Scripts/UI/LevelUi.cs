using GeometricFlight.Core;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>In-level UI: HUD readouts, pause menu (with Options and Help), and results.
    /// Observes LevelFlow and PlayerFlight; owns no gameplay rules.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class LevelUi : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;
        [SerializeField] private PlayerFlight flight;
        [Tooltip("Lets the ship's destruction play before the results appear.")]
        [SerializeField, Min(0f)] private float failedResultsDelay = 1.4f;
        [SerializeField, Min(0f)] private float completedResultsDelay = 0.6f;

        private VisualElement screen, hud, pausePanel, resultsPanel, helpPanel;
        private VisualElement throttle, throttleFill, throttleSetpoint, throttleCruise, throttleBand, parRow;
        private Label timeLabel, targetsLabel, speedLabel, throttleCaption, throttleHint, zoneBanner, resultsTitle;
        private Button resumeButton, nextButton, retryButton, helpBack;
        private OptionsPanel options;
        private int shownTenths = -1, shownTargets = -1, shownSpeed = -1;
        private ThrottleZone[] zones;
        private ThrottleZone shownZone;
        private string shownHint;
        private IVisualElementScheduledItem hideBanner;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            screen = root.Q("screen");
            hud = root.Q("hud");
            pausePanel = root.Q("pause-panel");
            resultsPanel = root.Q("results-panel");
            helpPanel = root.Q("help-panel");
            options = new OptionsPanel(root.Q("options-panel"), ShowPause);
            timeLabel = root.Q<Label>("hud-time");
            targetsLabel = root.Q<Label>("hud-targets");
            speedLabel = root.Q<Label>("throttle-speed");
            throttle = root.Q("throttle");
            throttleCaption = root.Q<Label>("throttle-caption");
            throttleHint = root.Q<Label>("throttle-hint");
            throttleBand = root.Q("throttle-band");
            zoneBanner = root.Q<Label>("zone-banner");
            parRow = root.Q("par-row");
            throttleFill = root.Q("throttle-fill");
            throttleSetpoint = root.Q("throttle-setpoint");
            throttleCruise = root.Q("throttle-cruise");
            resultsTitle = root.Q<Label>("results-title");
            resumeButton = root.Q<Button>("resume-button");
            nextButton = root.Q<Button>("next-button");
            retryButton = root.Q<Button>("retry-button");
            helpBack = root.Q<Button>("help-back");

            resumeButton.clicked += flow.Resume;
            root.Q<Button>("pause-retry-button").clicked += flow.Retry;
            root.Q<Button>("pause-options-button").clicked +=
                () => ShowOverlay(options.Root, options.FirstFocus);
            root.Q<Button>("pause-help-button").clicked += () => ShowOverlay(helpPanel, helpBack);
            root.Q<Button>("pause-menu-button").clicked += flow.ExitToMenu;
            helpBack.clicked += ShowPause;
            nextButton.clicked += flow.Continue;
            retryButton.clicked += flow.Retry;
            root.Q<Button>("results-menu-button").clicked += flow.ExitToMenu;

            throttleCruise.style.left = Length.Percent(SpeedFraction(flight.CruiseSpeed) * 100f);
            zones = FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);
            ShowZone(null);
            flow.StateChanged += OnStateChanged;
            ShowOverlay(null, null);
        }

        private void OnDisable()
        {
            if (flow != null) flow.StateChanged -= OnStateChanged;
        }

        private void Update()
        {
            int tenths = Mathf.FloorToInt(flow.ElapsedTime * 10f);
            if (tenths != shownTenths)
            {
                shownTenths = tenths;
                timeLabel.text = UiScreens.FormatTime(flow.ElapsedTime);
            }
            if (flow.TargetsDestroyed != shownTargets)
            {
                shownTargets = flow.TargetsDestroyed;
                targetsLabel.text = $"TARGETS {flow.TargetsDestroyed}/{flow.TargetsTotal}";
            }

            throttleFill.style.width = Length.Percent(SpeedFraction(flight.CurrentSpeed) * 100f);
            throttleSetpoint.style.left = Length.Percent(SpeedFraction(flight.TargetSpeed) * 100f);
            int speedPercent = Mathf.RoundToInt(flight.CurrentSpeed / flight.CruiseSpeed * 100f);
            if (speedPercent != shownSpeed)
            {
                shownSpeed = speedPercent;
                speedLabel.text = $"SPEED {speedPercent}%";
            }
            UpdateThrottleGuidance();
        }

        /// <summary>Inside a ThrottleZone the gauge shows the recommended band and a hint.</summary>
        private void UpdateThrottleGuidance()
        {
            ThrottleZone zone = null;
            float z = flight.transform.position.z;
            foreach (ThrottleZone candidate in zones)
                if (candidate.Contains(z)) { zone = candidate; break; }
            if (zone != shownZone) ShowZone(zone);
            if (zone == null) return;

            Vector2 band = zone.RecommendedCruiseMultiple * flight.CruiseSpeed;
            string hint = flight.TargetSpeed < band.x - 0.01f ? "RAISE  [E]"
                : flight.TargetSpeed > band.y + 0.01f ? "LOWER  [Q]" : "HOLD";
            if (hint == shownHint) return;
            shownHint = hint;
            throttleHint.text = hint;
            throttle.EnableInClassList("throttle--in-band", hint == "HOLD");
        }

        /// <summary>Brief top-of-screen callout when a zone starts, so the floor cues are not missed.</summary>
        private void AnnounceZone(ThrottleZone zone)
        {
            zoneBanner.text = zone.Type == ThrottleZone.Kind.Slow
                ? "SLOW ZONE  ·  LOWER THROTTLE" : "FAST ZONE  ·  FULL THROTTLE";
            zoneBanner.AddToClassList("zone-banner--visible");
            hideBanner?.Pause();
            hideBanner = zoneBanner.schedule.Execute(() => zoneBanner.RemoveFromClassList("zone-banner--visible"));
            hideBanner.ExecuteLater(3000);
        }

        private void ShowZone(ThrottleZone zone)
        {
            shownZone = zone;
            shownHint = null;
            bool guided = zone != null;
            throttle.EnableInClassList("throttle--guided", guided);
            throttleBand.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
            throttleHint.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
            throttleCaption.text = !guided ? "THROTTLE"
                : zone.Type == ThrottleZone.Kind.Slow ? "THROTTLE · SLOW ZONE" : "THROTTLE · FAST ZONE";
            if (!guided) return;
            AnnounceZone(zone);
            float from = SpeedFraction(zone.RecommendedCruiseMultiple.x * flight.CruiseSpeed);
            float to = SpeedFraction(zone.RecommendedCruiseMultiple.y * flight.CruiseSpeed);
            throttleBand.style.left = Length.Percent(from * 100f);
            throttleBand.style.width = Length.Percent((to - from) * 100f);
        }

        private float SpeedFraction(float speed) =>
            Mathf.InverseLerp(flight.MinSpeed, flight.MaxSpeed, speed);

        private void OnStateChanged(RunState state)
        {
            switch (state)
            {
                case RunState.Playing: ShowOverlay(null, null); break;
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

        private void ShowResults()
        {
            bool completed = flow.State == RunState.Completed;
            resultsTitle.text = completed ? "LEVEL COMPLETE" : "SHIP DESTROYED";
            SetStat("result-time", UiScreens.FormatTime(flow.ElapsedTime));
            SetStat("result-targets", $"{flow.TargetsDestroyed} / {flow.TargetsTotal}");
            SetStat("result-accuracy", flow.ShotsFired == 0 ? "-" : $"{Mathf.RoundToInt(flow.Accuracy * 100f)}%");
            SetStat("result-shield", $"{flow.ShieldRemaining} / {flow.ShieldMax}");
            bool showPar = completed && flow.HasPar;
            parRow.style.display = showPar ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPar)
                SetStat("result-par", UiScreens.FormatTime(flow.ParTime)
                    + (flow.ElapsedTime <= flow.ParTime ? "  BEATEN" : "  MISSED"));
            bool canContinue = completed && flow.HasNextLevel;
            nextButton.style.display = canContinue ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = DisplayStyle.None;
            ShowOverlay(resultsPanel, canContinue ? nextButton : retryButton);
        }

        private void SetStat(string name, string value) => resultsPanel.Q<Label>(name).text = value;

        private void ShowOverlay(VisualElement panel, VisualElement focus)
        {
            screen.EnableInClassList("screen--dimmed", panel != null);
            UiScreens.ShowOnly(panel, focus, pausePanel, resultsPanel, options.Root, helpPanel);
        }
    }
}

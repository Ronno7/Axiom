using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>In-level UI: HUD readouts, pause menu (with Options and Help), results, and the
    /// controller LCD. Observes LevelFlow and PlayerFlight; owns no gameplay rules.</summary>
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
        private Label controllerStatus, pauseNotice, proximityLabel;
        private string shownController;
        private Button resumeButton, nextButton, retryButton, helpBack;
        private OptionsPanel options;
        private CalibrationPanel calibration;
        private CockpitDisplay cockpit;
        private readonly ProximityAlarm proximity = new ProximityAlarm();
        private ShipHull hull;
        private ShipShield shield;
        private DamageFeedback damageFeedback;
        private int shownTenths = -1, shownTargets = -1, shownSpeed = -1;
        private ThrottleZone[] zones;
        private ThrottleZone shownZone;
        private Vector2 shownBand;
        private int guidance;
        private string shownHint;
        private IVisualElementScheduledItem hideBanner;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            screen = root.Q("screen");
            hud = root.Q("hud");
            damageFeedback = root.Q<DamageFeedback>("damage-feedback");
            damageFeedback.ResetPulse();
            shield = flight.GetComponent<ShipShield>();
            if (shield != null) shield.Changed += OnShieldHit;
            pausePanel = root.Q("pause-panel");
            resultsPanel = root.Q("results-panel");
            helpPanel = root.Q("help-panel");
            options = new OptionsPanel(root.Q("options-panel"), ShowPause, ShowCalibration);
            calibration = new CalibrationPanel(root.Q("calibration-panel"), ShowOptions);
            timeLabel = root.Q<Label>("hud-time");
            targetsLabel = root.Q<Label>("hud-targets");
            speedLabel = root.Q<Label>("throttle-speed");
            throttle = root.Q("throttle");
            throttleCaption = root.Q<Label>("throttle-caption");
            throttleHint = root.Q<Label>("throttle-hint");
            throttleBand = root.Q("throttle-band");
            zoneBanner = root.Q<Label>("zone-banner");
            controllerStatus = root.Q<Label>("hud-controller");
            proximityLabel = root.Q<Label>("hud-proximity");
            pauseNotice = root.Q<Label>("pause-notice");
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
            root.Q<Button>("pause-options-button").clicked += ShowOptions;
            root.Q<Button>("pause-help-button").clicked += () => ShowOverlay(helpPanel, helpBack);
            root.Q<Button>("pause-menu-button").clicked += flow.ExitToMenu;
            helpBack.clicked += ShowPause;
            nextButton.clicked += flow.Continue;
            retryButton.clicked += flow.Retry;
            root.Q<Button>("results-menu-button").clicked += flow.ExitToMenu;

            throttleCruise.style.left = Length.Percent(SpeedFraction(flight.CruiseSpeed) * 100f);
            zones = FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);
            cockpit = new CockpitDisplay(flow, flight);
            hull = flight.GetComponent<ShipHull>();
            ShowZone(null);
            flow.StateChanged += OnStateChanged;
            ShowOverlay(null, null);
        }

        private void OnDisable()
        {
            if (flow != null) flow.StateChanged -= OnStateChanged;
            if (shield != null) shield.Changed -= OnShieldHit;
            damageFeedback?.ResetPulse();
        }

        private void OnShieldHit(int remaining) => damageFeedback.Pulse(remaining == 0);

        private void Update()
        {
            damageFeedback.Tick(Time.unscaledDeltaTime);
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
            UpdateControllerStatus();
            UpdateProximity();
            cockpit.Show(shownZone, shownBand, guidance, proximity, UiScreens.FocusedLabel(screen));
            calibration.Tick(); // Replaces the cockpit on the LCD while open.
        }

        private void ShowOptions() => ShowOverlay(options.Root, options.FirstFocus);

        private void ShowCalibration()
        {
            calibration.Open();
            ShowOverlay(calibration.Root, calibration.FirstFocus);
        }

        /// <summary>Which device is driving the ship, and a warning (paired with a buzzer cue) when the
        /// throttle sensor has no signal. While paused by a disconnect, the pause panel explains it.</summary>
        private void UpdateControllerStatus()
        {
            ControllerState controller = ControllerService.Instance.State;
            bool warning = controller.Connected && !controller.ThrottleValid;
            string text = !controller.Connected ? "KEYBOARD" : warning ? "THROTTLE SENSOR: NO SIGNAL" : "CONTROLLER";
            if (text != shownController)
            {
                shownController = text;
                controllerStatus.text = text;
                controllerStatus.EnableInClassList("hud-controller--warning", warning);
            }

            bool lost = flow.State == RunState.Paused && flow.PauseReason == PauseReason.ControllerLost;
            pauseNotice.style.display = lost ? DisplayStyle.Flex : DisplayStyle.None;
            if (lost)
                pauseNotice.text = controller.Connected
                    ? "Controller reconnected. Resume when ready."
                    : "Controller disconnected. Reconnect it, or resume with the keyboard.";
        }

        /// <summary>Parking-sensor beeps (ProximityAlarm) with a matching on-screen tag that pulses in time.</summary>
        private void UpdateProximity()
        {
            bool flying = flow.State == RunState.Playing && flight.isActiveAndEnabled && !flight.InputLocked;
            proximity.Update(flight, hull, flying && hull != null, Time.unscaledTime);
            proximityLabel.style.display = proximity.Active ? DisplayStyle.Flex : DisplayStyle.None;
            proximityLabel.EnableInClassList("hud-proximity--pulse", proximity.Pulse);
        }

        /// <summary>Inside a ThrottleZone the gauge shows the recommended band and a hint.</summary>
        private void UpdateThrottleGuidance()
        {
            ThrottleZone zone = null;
            float z = flight.transform.position.z;
            foreach (ThrottleZone candidate in zones)
                if (candidate.Contains(z)) { zone = candidate; break; }
            if (zone != shownZone) ShowZone(zone);
            guidance = 0;
            if (zone == null) return;

            Vector2 band = zone.RecommendedCruiseMultiple * flight.CruiseSpeed;
            guidance = flight.TargetSpeed < band.x - 0.01f ? 1 : flight.TargetSpeed > band.y + 0.01f ? -1 : 0;
            bool keys = !ControllerService.Instance.State.Connected;
            string hint = guidance > 0 ? (keys ? "RAISE  [E]" : "RAISE")
                : guidance < 0 ? (keys ? "LOWER  [Q]" : "LOWER") : "HOLD";
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
            shownBand = new Vector2(from, to);
            throttleBand.style.left = Length.Percent(from * 100f);
            throttleBand.style.width = Length.Percent((to - from) * 100f);
        }

        private float SpeedFraction(float speed) =>
            Mathf.InverseLerp(flight.MinSpeed, flight.MaxSpeed, speed);

        private void OnStateChanged(RunState state)
        {
            switch (state)
            {
                case RunState.Playing:
                    // Resuming (e.g. Esc) mid-calibration abandons it and restores the old values.
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

        private void ShowResults()
        {
            // The numbers captured when the run ended (the cockpit LCD shows the same ones).
            RunResults results = flow.Results;
            bool completed = results.Completed;
            resultsTitle.text = completed ? "LEVEL COMPLETE" : "SHIP DESTROYED";
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
            ShowOverlay(resultsPanel, canContinue ? nextButton : retryButton);
        }

        private void SetStat(string name, string value) => resultsPanel.Q<Label>(name).text = value;

        private void ShowOverlay(VisualElement panel, VisualElement focus)
        {
            if (panel != null) damageFeedback.ResetPulse();
            screen.EnableInClassList("screen--dimmed", panel != null);
            UiScreens.ShowOnly(panel, focus, pausePanel, resultsPanel, options.Root, helpPanel, calibration.Root);
        }
    }
}

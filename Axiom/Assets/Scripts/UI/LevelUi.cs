using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>In-level UI: HUD readouts, pause menu (with Options and Help), results, and the
    /// controller LCD. Observes LevelFlow and ShipFlight; owns no gameplay rules.</summary>
    [DefaultExecutionOrder(100)] // Read shield/run state only after the gameplay components initialize.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class LevelUi : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;
        [SerializeField] private ShipFlight flight;
        [Tooltip("Lets the ship's destruction play before the results appear.")]
        [SerializeField, Min(0f)] private float failedResultsDelay = 1.4f;
        [SerializeField, Min(0f)] private float completedResultsDelay = 0.6f;

        private VisualElement screen, hud, pausePanel, resultsPanel, helpPanel;
        private VisualElement throttle, throttleTrack, throttleReadouts, throttleFill, throttleSetpoint, throttleCruise,
            throttleBand, throttleBoostZone, throttleReserve, throttleReserveFill, parRow;
        private Label timeLabel, targetsLabel, speedLabel, throttleCaption, throttleHint, resultsTitle,
            throttleReserveState;
        private string shownSpeedWord;
        private BoostStatus shownBoost = (BoostStatus)(-1);
        private Label controllerStatus, pauseNotice, proximityLabel;
        private string shownController;
        private Button resumeButton, nextButton, retryButton, resultsMenuButton, helpBack;
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
        private float entryVisibility = 1f, throttleBuild = 1f, controllerBuild = 1f;
        private InstrumentBuild timeBuild, targetsBuild, controllerTag, ringsBuild;
        private Label ringsLabel;
        private int courseDestroyed = -1, courseTotal, shownTargetTotal = -1;
        private VisualElement shieldRow;
        private ShieldGauge shieldGauge;
        private VisualElement shownPanel;

        private Label shieldNote;
        private IVisualElementScheduledItem hideShieldNote;

        /// <summary>The level's announcement panel (null clears it). Its lessons also speak for the throttle zones, so
        /// their calls stay on the gauge.</summary>
        public System.Action<string> AddressSink { get; set; }

        /// <summary>Opt-in shared-menu handoff. The level remains bound while the menu owns the LCD.</summary>
        public bool CockpitEnabled { get; set; } = true;

        /// <summary>Existing instruments have received a current state sample and are fully visible.</summary>
        public bool EntryReady => hud != null && entryVisibility >= 1f && throttleBuild >= 1f && controllerBuild >= 1f
            && shownTenths >= 0
            && shownTargets >= 0 && shownSpeed >= 0 && shownController != null;

        /// <summary>Actual flight feedback takes precedence over optional entry decoration.</summary>
        public bool HasPriorityNotice => flow != null && (flow.State == RunState.Paused
            || flow.State == RunState.Completed || flow.State == RunState.Failed)
            || shownZone != null || proximity.Active
            || shield != null && shield.State != null && shield.State.IsProtected;

        /// <summary>The tutorial's rings readout under the targets; null hides it. Other courses never show it.</summary>
        public void SetRingsReadout(string text)
        {
            if (ringsLabel == null) return;
            ringsLabel.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (text != null) ringsBuild.Text = text;
        }

        /// <summary>Counts this course's own targets instead of the whole run's (an arena after its rail, which starts
        /// over). The results still carry the whole run.</summary>
        public void SetTargetCount(int destroyed, int total)
        {
            courseDestroyed = destroyed;
            courseTotal = total;
        }

        /// <summary>The one announcement slot; null clears it. Stays until replaced or cleared.</summary>
        public void SetBanner(string text)
        {
            hideBanner?.Pause();
            AddressSink?.Invoke(text);
        }

        /// <summary>A short note beside the shield gauge, for what changed it ("CHECKPOINT").</summary>
        public void ShowShieldNote(string text, float seconds = 2.5f)
        {
            if (shieldNote == null) return;
            hideShieldNote?.Pause();
            shieldNote.text = text;
            shieldNote.style.display = DisplayStyle.Flex;
            hideShieldNote = shieldNote.schedule.Execute(() => shieldNote.style.display = DisplayStyle.None);
            hideShieldNote.ExecuteLater((long)(seconds * 1000f));
        }

        /// <summary>Shows or hides the whole HUD at once (reduced entry, reset).</summary>
        public void SetEntryVisibility(float amount) => SetEntryAssembly(amount, amount, amount);

        /// <summary>Draws the original HUD in without restarting its bindings, menus or LCD writer. Each value runs
        /// 0–1: <paramref name="readouts"/> builds the time, then the targets (and fades the rest);
        /// <paramref name="throttleAssembly"/> the throttle gauge; <paramref name="controller"/> the input tag.</summary>
        public void SetEntryAssembly(float readouts, float throttleAssembly, float controller)
        {
            entryVisibility = Mathf.Clamp01(readouts);
            throttleBuild = Mathf.Clamp01(throttleAssembly);
            controllerBuild = Mathf.Clamp01(controller);
            ApplyEntryPresentation();
        }

        private void ApplyEntryPresentation()
        {
            if (hud == null) return;
            // Inline styles only while partial, so class-driven opacity resumes afterward.
            foreach (VisualElement child in hud.Children())
                if (child != throttle && child != timeLabel && child != targetsLabel && child != controllerStatus
                    && child != ringsLabel && !child.ClassListContains(InstrumentBuild.RuleClass))
                    child.style.opacity = Partial(entryVisibility);
            timeBuild.SetAmount(Mathf.InverseLerp(0f, 0.8f, entryVisibility));
            targetsBuild.SetAmount(Mathf.InverseLerp(0.2f, 1f, entryVisibility));
            ringsBuild.SetAmount(Mathf.InverseLerp(0.4f, 1f, entryVisibility));
            controllerTag.SetAmount(controllerBuild);

            float b = throttleBuild;
            throttleCaption.text = b >= 1f ? CaptionFor(shownZone)
                : Typed(CaptionFor(shownZone), Mathf.InverseLerp(0f, 0.3f, b));
            throttleTrack.style.scale = b >= 1f ? new StyleScale(StyleKeyword.Null)
                : new Scale(new Vector3(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, b)), 1f, 1f));
            // The marks register after the track is drawn, then live values fill in.
            float marks = b >= 0.55f ? 1f : 0f;
            throttleCruise.style.opacity = throttleSetpoint.style.opacity = Partial(b >= 1f ? 1f : marks);
            throttleFill.style.opacity = Partial(Mathf.InverseLerp(0.6f, 0.85f, b));
            throttleReadouts.style.opacity = throttleReserve.style.opacity = Partial(Mathf.InverseLerp(0.75f, 1f, b));
            shieldRow.style.opacity = Partial(Mathf.InverseLerp(0f, 0.4f, b));
        }

        private static StyleFloat Partial(float amount) =>
            amount >= 1f ? new StyleFloat(StyleKeyword.Null) : new StyleFloat(amount);

        private static string Typed(string text, float amount)
        {
            int count = Mathf.FloorToInt(text.Length * Mathf.Clamp01(amount));
            return count >= text.Length ? text : text.Substring(0, count) + (count > 0 ? "_" : string.Empty);
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            screen = root.Q("screen");
            hud = root.Q("hud");
            damageFeedback = root.Q<DamageFeedback>("damage-feedback");
            damageFeedback.ResetPulse();
            shield = flight.GetComponent<ShipShield>();
            if (shield != null) shield.Hit += OnShieldHit;
            pausePanel = root.Q("pause-panel");
            resultsPanel = root.Q("results-panel");
            helpPanel = root.Q("help-panel");
            options = new OptionsPanel(root.Q("options-panel"), ShowPause, ShowCalibration);
            calibration = new CalibrationPanel(root.Q("calibration-panel"), ShowOptions);
            timeLabel = root.Q<Label>("hud-time");
            targetsLabel = root.Q<Label>("hud-targets");
            speedLabel = root.Q<Label>("throttle-speed");
            throttle = root.Q("throttle");
            shieldRow = root.Q("shield");
            shieldNote = root.Q<Label>("shield-note");
            shieldNote.style.display = DisplayStyle.None;
            throttleTrack = throttle.Q(className: "throttle-track");
            throttleReadouts = throttle.Q(className: "throttle-readouts");
            throttleTrack.style.transformOrigin = new TransformOrigin(0f, Length.Percent(50f));
            throttleCaption = root.Q<Label>("throttle-caption");
            throttleHint = root.Q<Label>("throttle-hint");
            throttleBand = root.Q("throttle-band");
            controllerStatus = root.Q<Label>("hud-controller");
            timeBuild = new InstrumentBuild(timeLabel, false);
            targetsBuild = new InstrumentBuild(targetsLabel, true);
            ringsLabel = root.Q<Label>("hud-rings");
            ringsBuild = new InstrumentBuild(ringsLabel, true);
            controllerTag = new InstrumentBuild(controllerStatus, true);
            proximityLabel = root.Q<Label>("hud-proximity");
            pauseNotice = root.Q<Label>("pause-notice");
            parRow = root.Q("par-row");
            throttleFill = root.Q("throttle-fill");
            throttleSetpoint = root.Q("throttle-setpoint");
            throttleCruise = root.Q("throttle-cruise");
            throttleBoostZone = root.Q("throttle-boost-zone");
            throttleReserve = root.Q("throttle-reserve");
            throttleReserveFill = root.Q("throttle-reserve-fill");
            throttleReserveState = root.Q<Label>("throttle-reserve-state");
            resultsTitle = root.Q<Label>("results-title");
            resumeButton = root.Q<Button>("resume-button");
            nextButton = root.Q<Button>("next-button");
            retryButton = root.Q<Button>("retry-button");
            helpBack = root.Q<Button>("help-back");

            resumeButton.clicked += flow.Resume;
            root.Q<Button>("pause-retry-button").clicked += Retry;
            root.Q<Button>("pause-options-button").clicked += ShowOptions;
            root.Q<Button>("pause-help-button").clicked += () => ShowOverlay(helpPanel, helpBack);
            root.Q<Button>("pause-menu-button").clicked += flow.ExitToMenu;
            helpBack.clicked += ShowPause;
            nextButton.clicked += flow.Continue;
            retryButton.clicked += Retry;
            resultsMenuButton = root.Q<Button>("results-menu-button");
            resultsMenuButton.clicked += flow.ExitToMenu;

            BuildThrottleTrack();
            zones = FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);
            cockpit = new CockpitDisplay(flow, flight);
            hull = flight.GetComponent<ShipHull>();
            ShowZone(null);
            ApplyEntryPresentation();
            flow.StateChanged += OnStateChanged;
            flow.LeaveSubmenu = LeaveSubmenu;
            ShowOverlay(null, null);
        }

        private void OnDisable()
        {
            if (flow != null)
            {
                flow.StateChanged -= OnStateChanged;
                flow.LeaveSubmenu = null;
            }
            if (shield != null) shield.Hit -= OnShieldHit;
            damageFeedback?.ResetPulse();
        }

        private void OnShieldHit(int remaining) => damageFeedback.Pulse(remaining == 0);

        private void UpdateShield()
        {
            if (shield == null || shield.State == null)
            {
                shieldRow.style.display = DisplayStyle.None;
                return;
            }
            shieldGauge ??= new ShieldGauge(shieldRow.Q("shield-segments"), shield.State.Max);
            shieldGauge.Show(shield.State.Current);
            shieldGauge.Tick(Time.unscaledDeltaTime);
            shieldRow.EnableInClassList("shield--low", shield.State.Current == 1);
        }

        private void Retry()
        {
            var study = flight.GetComponent<RailHandlingStudy>();
            if (study != null && study.isActiveAndEnabled)
                study.Restart(study.Candidate, ((ArenaFlight)flight).FullRotation);
            else flow.Retry();
        }

        private void Update()
        {
            damageFeedback.Tick(Time.unscaledDeltaTime);
            int tenths = Mathf.FloorToInt(flow.ElapsedTime * 10f);
            if (tenths != shownTenths)
            {
                shownTenths = tenths;
                timeBuild.Text = UiScreens.FormatTime(flow.ElapsedTime);
            }
            int destroyed = courseDestroyed >= 0 ? courseDestroyed : flow.TargetsDestroyed;
            int total = courseDestroyed >= 0 ? courseTotal : flow.TargetsTotal;
            if (destroyed != shownTargets || total != shownTargetTotal)
            {
                shownTargets = destroyed;
                shownTargetTotal = total;
                targetsBuild.Text = $"TARGETS {destroyed}/{total}";
            }
            UpdateShield();

            throttleFill.style.width = Length.Percent(SpeedFraction(flight.CurrentSpeed) * 100f);
            throttleSetpoint.style.left = Length.Percent(ThrottleGauge.LeverFraction(flight.Lever) * 100f);
            int speed = Mathf.RoundToInt(flight.CurrentSpeed);
            string word = ThrottleGauge.Word(flight);
            if (speed != shownSpeed || word != shownSpeedWord)
            {
                shownSpeed = speed;
                shownSpeedWord = word;
                speedLabel.text = word.Length > 0 ? $"SPEED {speed} M/S · {word}" : $"SPEED {speed} M/S";
            }
            UpdateBoostReserve();
            UpdateThrottleGuidance();
            UpdateControllerStatus();
            UpdateProximity();
            if (CockpitEnabled)
            {
                cockpit.Show(shownZone, shownBand, guidance, proximity, UiScreens.FocusedLabel(screen));
                calibration.Tick(); // Replaces the cockpit on the LCD while open.
            }
        }

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

        /// <summary>Which device is driving the ship, and a warning (paired with a buzzer cue) when the
        /// throttle sensor has no signal. While paused by a disconnect, the pause panel explains it.</summary>
        private void UpdateControllerStatus()
        {
            ControllerState controller = ControllerService.Instance.State;
            bool keyboardThrottle = GameSettings.KeyboardThrottleWithController;
            bool warning = controller.Connected && !keyboardThrottle && !controller.ThrottleValid;
            string text = !controller.Connected ? "KEYBOARD"
                : keyboardThrottle ? "CONTROLLER + KEYBOARD THROTTLE"
                : warning ? "THROTTLE SENSOR: NO SIGNAL" : "CONTROLLER";
            if (text != shownController)
            {
                shownController = text;
                controllerTag.Text = text;
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
            // Only ships whose motion can be predicted honestly (the slide rail, nose-led rails) warn.
            proximity.Update(flight, hull, flying && flight.CanPredict && hull != null, Time.unscaledTime);
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
            bool keys = !ControllerService.Instance.State.Connected || GameSettings.KeyboardThrottleWithController;
            string hint = guidance > 0 ? (keys ? "RAISE  [E]" : "RAISE")
                : guidance < 0 ? (keys ? "LOWER  [Q]" : "LOWER") : "HOLD";
            if (hint == shownHint) return;
            shownHint = hint;
            throttleHint.text = hint;
            throttle.EnableInClassList("throttle--in-band", hint == "HOLD");
        }

        /// <summary>A passing announcement in the one slot.</summary>
        public void Announce(string text, float seconds = 3f)
        {
            if (screen == null) return;
            SetBanner(text);
            hideBanner = screen.schedule.Execute(() => SetBanner(null));
            hideBanner.ExecuteLater((long)(seconds * 1000f));
        }

        private void ShowZone(ThrottleZone zone)
        {
            shownZone = zone;
            shownHint = null;
            bool guided = zone != null;
            throttle.EnableInClassList("throttle--guided", guided);
            throttleBand.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
            throttleHint.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
            if (throttleBuild >= 1f) throttleCaption.text = CaptionFor(zone);
            if (!guided) return;
            float from = SpeedFraction(zone.RecommendedCruiseMultiple.x * flight.CruiseSpeed);
            float to = SpeedFraction(zone.RecommendedCruiseMultiple.y * flight.CruiseSpeed);
            shownBand = new Vector2(from, to);
            throttleBand.style.left = Length.Percent(from * 100f);
            throttleBand.style.width = Length.Percent((to - from) * 100f);
        }

        private static string CaptionFor(ThrottleZone zone) => zone == null ? "THROTTLE"
            : zone.Type == ThrottleZone.Kind.Slow ? "THROTTLE · SLOW ZONE" : "THROTTLE · FAST ZONE";

        private float SpeedFraction(float speed) => ThrottleGauge.SpeedFraction(flight, speed);

        /// <summary>Eight segments like the cockpit's lights, the cruise notch at the centre, and the boost gate
        /// (normal full throttle) with the boost stretch beyond it.</summary>
        private void BuildThrottleTrack()
        {
            float gate = ThrottleGauge.LeverFraction(flight.BoostGate);
            bool boost = flight.Boost != BoostStatus.Off;
            for (int k = 1; k < ThrottleGauge.Segments; k++)
            {
                float at = k / (float)ThrottleGauge.Segments;
                if (Mathf.Approximately(at, 0.5f)) continue; // The cruise notch.
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("throttle-tick");
                tick.EnableInClassList("throttle-tick--gate", boost && Mathf.Approximately(at, gate));
                tick.style.left = Length.Percent(at * 100f);
                throttleTrack.Insert(throttleTrack.IndexOf(throttleFill) + 1, tick); // Over the fill.
            }
            throttleCruise.style.left = Length.Percent(50f);
            throttleBoostZone.style.display = boost ? DisplayStyle.Flex : DisplayStyle.None;
            throttleBoostZone.style.left = Length.Percent(gate * 100f);
            throttleReserve.style.display = boost ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateBoostReserve()
        {
            BoostStatus status = flight.Boost;
            if (status == BoostStatus.Off) return;
            throttleReserveFill.style.width = Length.Percent(flight.BoostCharge * 100f);
            if (status == shownBoost) return;
            shownBoost = status;
            throttleReserveState.text = ThrottleGauge.ReserveText(status);
            throttle.EnableInClassList("throttle--boosting", status == BoostStatus.Active);
            throttle.EnableInClassList("throttle--boost-ready", status == BoostStatus.Ready);
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
                    SetBanner(null);
                    screen.schedule.Execute(ShowResults).ExecuteLater((long)(completedResultsDelay * 1000f));
                    break;
                case RunState.Failed:
                    SetBanner(null);
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

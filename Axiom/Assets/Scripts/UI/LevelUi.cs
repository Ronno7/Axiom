using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>In-level UI: owns the document, the HUD's readouts and the controller LCD, with the throttle gauge
    /// (<see cref="ThrottleInstrument"/>) and menus (<see cref="RunMenus"/>) as parts. Observes LevelFlow and ShipFlight;
    /// owns no gameplay rules.</summary>
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

        private VisualElement screen, hud, shieldRow;
        private Label timeLabel, targetsLabel, ringsLabel, controllerStatus, pauseNotice, proximityLabel, shieldNote;
        private ThrottleInstrument throttle;
        private RunMenus menus;
        private CockpitDisplay cockpit;
        private ShieldGauge shieldGauge;
        private DamageFeedback damageFeedback;
        private InstrumentBuild timeBuild, targetsBuild, controllerTag, ringsBuild;
        private readonly ProximityAlarm proximity = new ProximityAlarm();
        private ShipHull hull;
        private ShipShield shield;
        private IVisualElementScheduledItem hideBanner, hideShieldNote;
        private string shownController;
        private int shownTenths = -1, shownTargets = -1, shownTargetTotal = -1, courseDestroyed = -1, courseTotal;
        private float entryVisibility = 1f, controllerBuild = 1f;

        /// <summary>The level's announcement panel (null clears it). Its lessons also speak for the throttle zones, so
        /// their calls stay on the gauge.</summary>
        public System.Action<string> AddressSink { get; set; }

        /// <summary>Off while the menu owns the LCD. The level stays bound.</summary>
        public bool CockpitEnabled { get; set; } = true;

        /// <summary>Existing instruments have received a current state sample and are fully visible.</summary>
        public bool EntryReady => hud != null && entryVisibility >= 1f && throttle.Built && controllerBuild >= 1f
            && shownTenths >= 0 && shownTargets >= 0 && throttle.HasReading && shownController != null;

        /// <summary>Actual flight feedback takes precedence over optional entry decoration.</summary>
        public bool HasPriorityNotice => flow != null && (flow.State == RunState.Paused
            || flow.State == RunState.Completed || flow.State == RunState.Failed)
            || throttle?.Zone != null || proximity.Active
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

        /// <summary>A passing announcement in the one slot.</summary>
        public void Announce(string text, float seconds = 3f)
        {
            if (screen == null) return;
            SetBanner(text);
            hideBanner = screen.schedule.Execute(() => SetBanner(null));
            hideBanner.ExecuteLater((long)(seconds * 1000f));
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
            controllerBuild = Mathf.Clamp01(controller);
            throttle?.SetBuild(throttleAssembly);
            ApplyEntryPresentation(Mathf.Clamp01(throttleAssembly));
        }

        private void ApplyEntryPresentation(float throttleBuild)
        {
            if (hud == null) return;
            // Inline styles only while partial, so class-driven opacity resumes afterward.
            foreach (VisualElement child in hud.Children())
                if (child != throttle.Root && child != timeLabel && child != targetsLabel && child != controllerStatus
                    && child != ringsLabel && !child.ClassListContains(InstrumentBuild.RuleClass))
                    child.style.opacity = InstrumentBuild.Partial(entryVisibility);
            timeBuild.SetAmount(Mathf.InverseLerp(0f, 0.8f, entryVisibility));
            targetsBuild.SetAmount(Mathf.InverseLerp(0.2f, 1f, entryVisibility));
            ringsBuild.SetAmount(Mathf.InverseLerp(0.4f, 1f, entryVisibility));
            controllerTag.SetAmount(controllerBuild);
            shieldRow.style.opacity = InstrumentBuild.Partial(Mathf.InverseLerp(0f, 0.4f, throttleBuild));
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
            timeLabel = root.Q<Label>("hud-time");
            targetsLabel = root.Q<Label>("hud-targets");
            shieldRow = root.Q("shield");
            shieldNote = root.Q<Label>("shield-note");
            shieldNote.style.display = DisplayStyle.None;
            controllerStatus = root.Q<Label>("hud-controller");
            timeBuild = new InstrumentBuild(timeLabel, false);
            targetsBuild = new InstrumentBuild(targetsLabel, true);
            ringsLabel = root.Q<Label>("hud-rings");
            ringsBuild = new InstrumentBuild(ringsLabel, true);
            controllerTag = new InstrumentBuild(controllerStatus, true);
            proximityLabel = root.Q<Label>("hud-proximity");
            pauseNotice = root.Q<Label>("pause-notice");

            throttle = new ThrottleInstrument(root, flight);
            cockpit = new CockpitDisplay(flow, flight);
            hull = flight.GetComponent<ShipHull>();
            ApplyEntryPresentation(1f);
            flow.StateChanged += OnStateChanged;
            menus = new RunMenus(root, flow, damageFeedback, failedResultsDelay, completedResultsDelay);
        }

        private void OnDisable()
        {
            if (flow != null)
            {
                flow.StateChanged -= OnStateChanged;
                menus?.Detach();
            }
            if (shield != null) shield.Hit -= OnShieldHit;
            damageFeedback?.ResetPulse();
        }

        private void OnStateChanged(RunState state)
        {
            if (state == RunState.Completed || state == RunState.Failed) SetBanner(null);
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
            throttle.Tick();
            UpdateControllerStatus();
            UpdateProximity();
            if (CockpitEnabled)
            {
                cockpit.Show(throttle.Zone, throttle.Band, throttle.Guidance, proximity, UiScreens.FocusedLabel(screen));
                menus.TickCalibration();
            }
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
            // Only on the rail: the ship's motion can be predicted honestly there.
            proximity.Update(flight, hull, flying && flight.CanPredict && hull != null, Time.unscaledTime);
            proximityLabel.style.display = proximity.Active ? DisplayStyle.Flex : DisplayStyle.None;
            proximityLabel.EnableInClassList("hud-proximity--pulse", proximity.Pulse);
        }
    }
}

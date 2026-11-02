using System.Collections.Generic;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GeometricFlight.Core
{
    /// <summary>An opt-in entry study over the existing rail run. LevelFlow remains the only run,
    /// pause and retry owner. This component relinquishes the actor and camera at admission.
    /// The menu sits at the passage's approach: one camera rises off the runway into the chase view
    /// while a terminal prints the passage into existence ahead of it (see EntrySequence).</summary>
    [DefaultExecutionOrder(40), DisallowMultipleComponent]
    public sealed class SharedEntryStudy : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;
        [SerializeField] private ArenaFlight flight;
        [SerializeField] private ChaseCamera chase;
        [SerializeField] private MainMenu menu;
        [SerializeField] private LevelUi levelUi;
        [SerializeField] private MenuLook look;
        [SerializeField] private MenuBackdrop backdrop;
        [SerializeField] private SharedEntryPresentation presentation;
        [SerializeField] private MissileDisplay missileDisplay;
        [SerializeField] private GameObject[] flightDetails;
        [SerializeField] private AudioSource voice;
        [SerializeField] private AudioClip shieldReport, controlReport;
        [SerializeField] private EnclosedPassage passage;
        [Tooltip("Plays from PLAY. The temporary score is written to the default tempo.")]
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioClip buildTheme;
        [Tooltip("Sixteen beats from PLAY to control: 96-120 BPM keeps the entry within 8-10 seconds.")]
        [SerializeField, Range(96f, 120f)] private float tempo = EntrySequence.DefaultTempo;
        [Tooltip("How far the menu's close haze opens up once the passage forms.")]
        [SerializeField, Min(1f)] private float passageFogScale = 3.5f;
        [Tooltip("Shown only once the camera is inside the passage (the arena beyond it), so the "
               + "menu keeps its own horizon.")]
        [SerializeField] private GameObject[] revealInside;
        [Tooltip("Course pieces inside the passage. Each appears once the walls around it are built, so "
               + "nothing floats outside unfinished walls.")]
        [SerializeField] private GameObject[] buildWithPassage;
        [Tooltip("The endless ground; it glides under the menu until PLAY.")]
        [SerializeField] private InfiniteGround ground;
        [Tooltip("Monospace font for the pop-up code windows.")]
        [SerializeField] private Font codeFont;
        [Tooltip("Both destinations use the same entry. Actual progression routing belongs to the later journey work.")]
        [SerializeField] private bool returning;

        private Camera view;
        private MasterArmInput authority;
        private ShipHull hull;
        private ShipShield shield;
        private VisualElement root, titlePanel, cover, threshold;
        private Label studyHint;
        private UIDocument menuDocument, levelDocument;
        private Vector3 cameraStart, admission;
        private Quaternion cameraRotation;
        private float cameraFov, clock, captionUntil;
        private float markingsFrom = float.NaN, ribsFrom = float.NaN, surfacesFrom = float.NaN;
        private bool started, transferred, admitted, reduced, shieldReported, audioPaused, tailComplete, backdropRetired;
        private EntryTerminal terminal;
        private EntryCodeTerminals codeWindows;
        private readonly List<Renderer> hiddenOutside = new List<Renderer>();
        private readonly List<Renderer> awaitingWalls = new List<Renderer>();
        private bool previousFire = true, previousConnection;
        private int coveredFrame = -1;
        private readonly List<DepartureElement> departure = new List<DepartureElement>();

        public float Elapsed => clock;
        public float Duration => reduced ? 1f : EntrySequence.Duration(tempo);
        public bool Started => started;
        public bool Transferred => transferred;
        public bool Admitted => admitted;
        public bool Reduced => reduced;
        public bool Returning => returning;
        public DepartureSample Current { get; private set; }

        private sealed class DepartureElement
        {
            public VisualElement Element;
            public Vector2 Translation;
            public float Depth;
        }

        private void Start()
        {
            if (flow == null || flight == null || chase == null || menu == null || levelUi == null ||
                look == null || backdrop == null || presentation == null || !presentation.IsConfigured ||
                missileDisplay == null || passage == null || !flow.EntryHeld)
            {
                Debug.LogError("The shared entry study is missing its held run or presentation references.", this);
                enabled = false;
                return;
            }
            view = chase.GetComponent<Camera>();
            authority = flight.GetComponent<MasterArmInput>();
            hull = flight.GetComponent<ShipHull>();
            shield = flight.GetComponent<ShipShield>();
            admission = flight.transform.position;
            cameraStart = view.transform.position;
            cameraRotation = view.transform.rotation;
            cameraFov = view.fieldOfView;
            chase.enabled = false;
            authority.PrepareRun(tutorial: true); // SAFE on the keyboard: the rail's weapons lesson teaches arming.
            hull.enabled = false;
            shield.TakesHits = false;
            foreach (GameObject detail in flightDetails) detail.SetActive(false);
            menuDocument = menu.GetComponent<UIDocument>();
            levelDocument = levelUi.GetComponent<UIDocument>();
            root = menuDocument.rootVisualElement;
            titlePanel = root.Q("title-panel");
            look.SetInterfaceDocuments(menuDocument, levelDocument);
            levelUi.CockpitEnabled = false;
            missileDisplay.enabled = false;
            reduced = GameSettings.ReducedEntryAnimation;
            presentation.Begin(reduced);
            BuildCover();
            terminal = new EntryTerminal(root);
            codeWindows = new EntryCodeTerminals(root, codeFont);
            passage.HideAll();
            if (revealInside != null)
                foreach (GameObject group in revealInside)
                    if (group != null)
                        foreach (Renderer part in group.GetComponentsInChildren<Renderer>())
                            if (part.enabled) { part.enabled = false; hiddenOutside.Add(part); }
            if (buildWithPassage != null)
                foreach (GameObject piece in buildWithPassage)
                    if (piece != null)
                        foreach (Renderer part in piece.GetComponentsInChildren<Renderer>())
                            if (part.enabled) { part.enabled = false; awaitingWalls.Add(part); }
            menu.PlayRequested += Begin;
            flow.StateChanged += OnRunState;
            shield.Changed += OnShieldChanged;
        }

        public void Begin()
        {
            if (!isActiveAndEnabled || started || root == null ||
                titlePanel.resolvedStyle.display == DisplayStyle.None || !presentation.IsConfigured) return;
            if (!flow.BeginEntry()) return;
            foreach (string name in new[] { "game-title", "play-button", "options-button", "help-button", "quit-button" })
            {
                VisualElement element = root.Q(name);
                Translate t = element.resolvedStyle.translate;
                departure.Add(new DepartureElement { Element = element,
                    Translation = new Vector2(t.x.value, t.y.value),
                    Depth = name == "game-title" ? 26f : 17f + departure.Count * 3f });
                element.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
                // Departing buttons sweep past the cursor; hovering them must not light them up.
                element.Query().ForEach(e => e.pickingMode = PickingMode.Ignore);
                // Nor can the stick or keyboard move focus onto them (FIRE must never press QUIT mid-flight).
                element.focusable = false;
            }
            (root.focusController?.focusedElement as VisualElement)?.Blur();
            started = true;
            if (!reduced && music != null && buildTheme != null)
            {
                music.clip = buildTheme;
                music.Play();
            }
            menu.Suspended = true;
            // The menu glides over the ground at the departure's starting speed; the camera takes the motion over.
            if (ground != null) ground.GlideSpeed = 0f;
            levelUi.CockpitEnabled = true;
            studyHint.style.display = DisplayStyle.None;
            look.StudyControlled = true;
            look.CameraSuppliesMotion = true;
            backdrop.SetPresentationMotion(true, cameraStart.z);
            // Menu input has ended. The existing run document now owns all navigation and pause UI.
            menuDocument.sortingOrder = levelDocument.sortingOrder - 1f;
            root.pickingMode = PickingMode.Ignore;
            titlePanel.pickingMode = PickingMode.Ignore;
        }

        private void Update()
        {
            if (root == null) return;
            if (!started)
            {
                ControllerState device = ControllerService.Instance.State;
                bool fire = device.Connected && device.Fire;
                if (previousConnection && fire && !previousFire &&
                    root.focusController.focusedElement == root.Q<Button>("play-button")) Begin();
                previousFire = fire;
                previousConnection = device.Connected;
                if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
                {
                    reduced = !reduced;
                    GameSettings.ReducedEntryAnimation = reduced;
                    GameSettings.Save();
                    presentation.Begin(reduced);
                }
                studyHint.text = reduced ? "ENTRY STUDY · REDUCED ANIMATION · F5 TO CHANGE"
                    : $"ENTRY STUDY · {Duration:0.#} SECOND ENTRY · F5 REDUCED ANIMATION";
                return;
            }
            bool paused = flow.State == RunState.Paused;
            look.PresentationPaused = paused;
            if (audioPaused != paused)
            {
                foreach (AudioSource source in new[] { voice, music })
                {
                    if (source == null) continue;
                    if (paused) source.Pause(); else source.UnPause();
                }
                audioPaused = paused;
            }
            if (flow.State != RunState.Playing)
            {
                if (paused && transferred) presentation.Sample(clock, Duration);
                return;
            }

            clock += Time.deltaTime;
            if (!admitted)
            {
                Current = MenuDepartureTimeline.Sample(clock, Duration, false, reduced, DepartureTreatment.SignalTransfer,
                    EntrySequence.CoverCenter);
                // Only the reduced quiet transfer still cuts the camera, so only it is covered.
                // The full entry is one continuous space: a scan line marks the threshold instead.
                float mask = reduced ? Current.Occlusion : 0f;
                float coverAt = Duration * (reduced ? 0.4f : EntrySequence.CoverCenter);
                if (!transferred && clock >= coverAt)
                {
                    if (reduced) mask = 1f;
                    if (coveredFrame < 0) coveredFrame = Time.frameCount;
                    if (!reduced || Time.frameCount > coveredFrame)
                    {
                        transferred = true;
                        view.farClipPlane = 2200f;
                        RevealInside();
                        if (reduced)
                        {
                            // The quiet transfer cuts straight to the finished passage.
                            RetireBackdrop();
                            passage.ShowComplete();
                            look.FogDistanceScale = passageFogScale;
                            look.RebuildAtmosphere();
                        }
                    }
                    else clock = coverAt;
                }
                cover.style.opacity = mask;
                SampleThreshold(coverAt);
                ApplyCameraAndShip();
                if (transferred) presentation.Sample(clock, Duration);
                SampleMissilePanel();
                ApplyDeparture();
                SampleBuild();
                if (!reduced && !shieldReported && clock >= Duration * 0.72f)
                {
                    shieldReported = true;
                    // No queue: a missed or no longer truthful report is simply omitted.
                    hull.enabled = true;
                    shield.TakesHits = true;
                    if (presentation.ShieldReady && shield.State.Current > 0 &&
                        clock + (shieldReport == null ? 1.35f : shieldReport.length) + 0.25f < Duration)
                        Report(shieldReport, "Shields online.");
                }
                if (clock >= Duration && transferred) Admit();
            }
            else if (!tailComplete)
            {
                presentation.Sample(clock, Duration);
                SampleBuild();
                tailComplete = clock >= Duration + presentation.TraceSeconds && passage.IsComplete;
            }
            if (clock >= captionUntil) presentation.SetCaption(null);
        }

        private void ApplyCameraAndShip()
        {
            look.DepartureAmount = reduced ? (transferred ? 1f : 0f) : Mathf.Clamp01(Current.Distance / 24f);
            // Dust streams past while the camera leaves the menu, then clears before the threshold scan.
            float threshold = Duration * EntrySequence.CoverCenter;
            look.DustVisibility = reduced ? (transferred ? 0f : 1f)
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 1.1f, threshold - 0.15f, clock));
            if (reduced && !transferred)
            {
                view.transform.SetPositionAndRotation(cameraStart, cameraRotation);
                view.fieldOfView = cameraFov;
                backdrop.SetPresentationMotion(true, cameraStart.z);
                return;
            }
            ArenaFlightState state = flight.State;
            state.Position = admission + Vector3.forward * (Mathf.Min(Current.Distance, 240f) - 240f);
            state.Speed = reduced ? MenuDepartureTimeline.ArrivalSpeed : Current.Speed;
            flight.StageEntry(state);
            chase.GetTargetPose(out Vector3 position, out Quaternion rotation, out float fov);
            if (reduced)
            {
                view.transform.SetPositionAndRotation(position, rotation);
                view.fieldOfView = fov;
                return;
            }
            // One continuous path: the menu camera travels with the (still hidden) ship and rises off
            // the runway into its chase view. Both share the departure distance, so nothing jumps.
            float progress = clock / Duration;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.62f, progress));
            Vector3 runway = cameraStart + Vector3.forward * Current.Distance;
            view.transform.SetPositionAndRotation(Vector3.Lerp(runway, position, rise),
                Quaternion.Slerp(cameraRotation, rotation, rise));
            view.fieldOfView = Mathf.Lerp(cameraFov, fov, rise)
                + Current.FovSurge * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.94f, progress)));
            if (!backdropRetired) backdrop.SetPresentationMotion(true, view.transform.position.z);
        }

        /// <summary>Construction, terminal and haze all follow the same clock as the camera.</summary>
        private void SampleBuild()
        {
            if (reduced)
            {
                terminal.Hide();
                codeWindows.Hide();
                RevealBuilt(float.PositiveInfinity);
                return;
            }
            float surfaces = FrontFromCamera(ref surfacesFrom, EntrySequence.SurfacesBeat, EntrySequence.SurfaceSpeed);
            RevealBuilt(surfaces);
            passage.SetFronts(FrontFromCamera(ref markingsFrom, EntrySequence.MarkingsBeat, EntrySequence.MarkingSpeed),
                FrontFromCamera(ref ribsFrom, EntrySequence.RibsBeat, EntrySequence.RibSpeed), surfaces);
            terminal.Sample(clock, tempo, LiveValue);
            codeWindows.Sample(clock, tempo, LiveValue);
            float beat = clock / EntrySequence.BeatLength(tempo);
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EntrySequence.MarkingsBeat, 10f, beat));
            look.FogDistanceScale = Mathf.Lerp(1f, passageFogScale, open);
        }

        /// <summary>Truthful values for the terminal's live rows.</summary>
        private string LiveValue(string key)
        {
            switch (key)
            {
                case "route": return returning ? "RETURN RUN" : "TRAINING";
                case "input":
                    ControllerState device = ControllerService.Instance.State;
                    return !device.Connected ? "KEYBOARD"
                        : GameSettings.KeyboardThrottleWithController ? "COCKPIT + KEYBOARD THROTTLE" : "COCKPIT";
                case "handling": return flight.FullRotation ? "ROLL & PITCH" : "BANK & TURN";
                case "passage": return $"PASSAGE {passage.Width:0} X {passage.Height:0} M";
                case "shield": return shield.State == null ? "-" : $"{shield.State.Current} / {shield.State.Max}";
                case "arm": return authority.Armed ? "ARMED" : "SAFE";
                case "throttle":
                    return (Mathf.Approximately(flight.Throttle, 0f) ? "CRUISE · " : string.Empty)
                        + $"{flight.TargetSpeed:0} M/S";
                // Code-window values: lower-case, as a program would print them.
                case "cockpit":
                    return ControllerService.Instance.State.Connected
                        ? "linked " + ControllerService.Instance.Link.PortName : "not found";
                case "invert": return GameSettings.InvertVertical ? "on" : "off";
                case "display": return GameSettings.ArcadeMode ? "crt" : "standard";
                case "width": return $"{passage.Width:0}";
                case "height": return $"{passage.Height:0}";
                case "length": return $"{passage.EndZ - passage.MouthZ:0}";
                case "mouth": return $"{passage.MouthZ:0}";
                case "ribs": return $"{passage.RibCount}";
                case "shieldMax": return shield.State == null ? "-" : $"{shield.State.Max}";
                case "limit":
                    RailHandlingStudy rail = flight.GetComponent<RailHandlingStudy>();
                    return rail != null ? $"{rail.HeadingLimit:0}" : "-";
                case "min": return $"{flight.MinSpeed:0}";
                case "max": return $"{flight.MaxSpeed:0}";
                case "cruise": return $"{flight.CruiseSpeed:0}";
                case "boost":
                    return flight.Boost == BoostStatus.Off ? "off"
                        : $"{flight.BoostSpeed:0} m/s burst, gate {flight.BoostGate:0.00}";
                default:
                    if (key.StartsWith("rib:") && int.TryParse(key.Substring(4), out int index))
                        return $"{passage.RibZ(index):0}";
                    return string.Empty;
            }
        }

        /// <summary>A construction front that starts just behind the camera on its row's beat (so the part forms
        /// around the viewer as it is announced) and then races ahead. -infinity before its beat.</summary>
        private float FrontFromCamera(ref float from, float beat, float speed)
        {
            float start = beat * EntrySequence.BeatLength(tempo);
            if (clock < start) return float.NegativeInfinity;
            if (float.IsNaN(from)) from = view.transform.position.z - 10f;
            return from + speed * (clock - start);
        }

        /// <summary>Shows course pieces whose walls now exist around them.</summary>
        private void RevealBuilt(float surfaceFront)
        {
            for (int i = awaitingWalls.Count - 1; i >= 0; i--)
            {
                Renderer part = awaitingWalls[i];
                if (part == null) { awaitingWalls.RemoveAt(i); continue; }
                if (part.bounds.max.z > surfaceFront) continue;
                part.enabled = true;
                awaitingWalls.RemoveAt(i);
            }
        }

        private void RevealInside()
        {
            foreach (Renderer part in hiddenOutside) if (part != null) part.enabled = true;
            hiddenOutside.Clear();
        }

        /// <summary>The weapons instrument draws itself in after the throttle (beats 13.4–15.2); weapons are
        /// still only released at control.</summary>
        private void SampleMissilePanel()
        {
            if (reduced) return;
            float amount = Mathf.InverseLerp(0.84f, 0.95f, clock / Duration);
            if (amount <= 0f) return;
            missileDisplay.Assembly = amount;
            if (!missileDisplay.enabled) missileDisplay.enabled = true;
        }

        private void RetireBackdrop()
        {
            if (backdropRetired) return;
            backdropRetired = true;
            backdrop.gameObject.SetActive(false);
        }

        private void Admit()
        {
            presentation.RestoreHull();
            hull.enabled = true;
            shield.TakesHits = true;
            if (!presentation.HullReady || !presentation.HudReady || flight.InputSampleFrame != Time.frameCount)
            {
                Debug.LogError("Entry admission stopped: hull, HUD or current input is not ready. Retry the study.", this);
                flow.Pause();
                return;
            }
            ArenaFlightState state = flight.State;
            state.Position = admission;
            state.Speed = MenuDepartureTimeline.ArrivalSpeed;
            flight.StageEntry(state);
            chase.ResetToTarget();
            if (!flow.TryBeginEntryRun()) return;
            admitted = true;
            authority.RequireRelease();
            authority.WeaponsAvailable = true;
            chase.enabled = true;
            missileDisplay.Assembly = 1f;
            missileDisplay.enabled = true;
            RetireBackdrop(); // Behind the camera by now; the passage floor continues it exactly.
            foreach (GameObject detail in flightDetails) detail.SetActive(true);
            cover.style.opacity = 0f;
            titlePanel.style.display = DisplayStyle.None;
            Report(controlReport, "Flight control online.");
            levelUi.Announce("WELCOME, USER.", 3.5f);
        }

        /// <summary>A passing message from the system (a lesson's idea, a rebuild) in the HUD's announcement slot.</summary>
        public void Announce(string line, float seconds)
        {
            if (flow.State != RunState.Playing || !admitted) return;
            levelUi.Announce(line, seconds);
        }

        private void Report(AudioClip clip, string caption)
        {
            if (flow.State != RunState.Playing || levelUi.HasPriorityNotice) return;
            if (voice != null)
            {
                voice.Stop();
                if (clip != null) { voice.clip = clip; voice.Play(); }
            }
            presentation.SetCaption(caption);
            captionUntil = clock + (clip != null ? clip.length : 1.8f);
        }

        private void OnShieldChanged(int remaining)
        {
            if (voice != null) voice.Stop();
            presentation.SuppressDecoration();
        }

        private void OnRunState(RunState state)
        {
            if (state != RunState.Failed && state != RunState.Completed) return;
            if (voice != null) voice.Stop();
            if (music != null) music.Stop();
            presentation.SetCaption(null);
            presentation.SuppressDecoration();
        }

        private void ApplyDeparture()
        {
            // The menu footer stays present without sharing the throttle's baseline during flight.
            VisualElement footer = root.Q("menu-footer");
            if (footer != null && transferred) footer.style.bottom = 10f;
            Vector2 center = root.worldBound.center;
            float ratio = Mathf.Tan(cameraFov * Mathf.Deg2Rad * 0.5f) / Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
            foreach (DepartureElement e in departure)
            {
                if (reduced) { e.Element.style.visibility = transferred ? Visibility.Hidden : Visibility.Visible; continue; }
                float remaining = e.Depth - Mathf.Max(0f, Current.Distance - 1f);
                if (remaining < 1f) { e.Element.style.visibility = Visibility.Hidden; continue; }
                Rect bounds = new Rect(e.Element.parent.LocalToWorld(e.Element.layout.position), e.Element.layout.size);
                float scale = e.Depth / remaining * ratio;
                Vector2 shift = (bounds.center + e.Translation - center) * (scale - 1f) + e.Translation;
                e.Element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                e.Element.style.translate = new Translate(shift.x, shift.y);
            }
        }

        /// <summary>A bright line sweeps down the screen as the camera crosses the passage mouth.</summary>
        private void SampleThreshold(float at)
        {
            const float sweep = 0.32f;
            float progress = (clock - (at - sweep * 0.5f)) / sweep;
            bool shown = !reduced && progress > 0f && progress < 1f;
            threshold.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown) threshold.style.top = Length.Percent(Mathf.Lerp(-12f, 104f, progress));
        }

        private void BuildCover()
        {
            cover = new VisualElement { name = "entry-cover", pickingMode = PickingMode.Ignore };
            cover.style.position = Position.Absolute;
            cover.style.left = cover.style.right = cover.style.top = cover.style.bottom = 0f;
            cover.style.backgroundColor = AxiomPalette.Void;
            cover.style.opacity = 0f;
            root.Add(cover);
            threshold = new VisualElement { name = "entry-threshold", pickingMode = PickingMode.Ignore };
            threshold.style.position = Position.Absolute;
            threshold.style.left = threshold.style.right = 0f;
            threshold.style.height = Length.Percent(10f);
            threshold.style.display = DisplayStyle.None;
            var wake = new VisualElement { pickingMode = PickingMode.Ignore };
            wake.style.flexGrow = 1f;
            wake.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Cyan, 0.1f);
            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.style.height = 3f;
            edge.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Light, 0.85f);
            threshold.Add(wake);
            threshold.Add(edge);
            root.Add(threshold);
            root.Q("frame")?.BringToFront();
            root.Q("menu-footer")?.BringToFront();
            studyHint = new Label { pickingMode = PickingMode.Ignore };
            studyHint.style.position = Position.Absolute;
            studyHint.style.right = 52f; studyHint.style.top = 52f;
            studyHint.style.color = AxiomPalette.Muted;
            studyHint.style.fontSize = 12f;
            root.Add(studyHint);
        }

        private void OnDestroy()
        {
            if (menu != null) menu.PlayRequested -= Begin;
            if (flow != null) flow.StateChanged -= OnRunState;
            if (shield != null) shield.Changed -= OnShieldChanged;
            if (voice != null) voice.Stop();
            if (music != null) music.Stop();
            terminal?.Remove();
            codeWindows?.Remove();
        }
    }
}

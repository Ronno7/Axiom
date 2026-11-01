using System;
using System.Collections.Generic;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>Isolated camera study. Reuses the authored menu and ends in moving reference scenery;
    /// it neither launches a run nor claims the production segment handoff.</summary>
    [DefaultExecutionOrder(-10), DisallowMultipleComponent]
    public sealed class MenuDepartureStudy : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField] private MainMenu menu;
        [SerializeField] private MenuLook look;
        [SerializeField] private MenuBackdrop backdrop;
        [SerializeField] private Transform world;
        [SerializeField] private Transform arrival;
        [SerializeField] private Transform[] frames;
        [SerializeField] private DepartureTreatment treatment;
        [SerializeField, Range(8f, 12f)] private float duration = 10f;
        [SerializeField] private bool returning, reduced;

        private Vector3 cameraStart, worldStart;
        private Quaternion rotationStart;
        private float fovStart, clock, idleClock;
        private bool started, paused, transferred, physicalFire, physicalMenu, controllerConnected, resumeNeedsRelease;
        private int coveredFrame = -1;
        private int pendingStartFrame = -1;
        private float forcedCover;
        private VisualElement root, mask, readout, controls, titlePanel;
        private Label heading, detail;
        private readonly List<DepthElement> depthElements = new List<DepthElement>();
        private GeometricConstruct[] constructs;
        private Vector3[] framePositions, frameScales;
        private Transform[] floorSurfaces;
        private Vector3[] floorPositions;
        private string pauseReason;

        public DepartureTreatment Treatment => treatment;
        public float Elapsed => clock;
        public float Duration => reduced ? 1f : returning ? 3f : duration;
        public bool Running => started;
        public bool Paused => paused;
        public bool Transferred => transferred;
        public bool Reduced => reduced;
        public bool Returning => returning;
        public DepartureSample Current { get; private set; }

        private sealed class DepthElement
        {
            public VisualElement Element;
            public Rect Bounds;
            public Vector2 Translation;
            public float Depth;
        }

        private void Start()
        {
            cameraStart = view.transform.position;
            rotationStart = view.transform.rotation;
            fovStart = view.fieldOfView;
            worldStart = world.position;
            root = GetComponent<UIDocument>().rootVisualElement;
            titlePanel = root.Q("title-panel");
            constructs = world.GetComponentsInChildren<GeometricConstruct>();
            framePositions = new Vector3[frames.Length];
            frameScales = new Vector3[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                framePositions[i] = frames[i].localPosition;
                frameScales[i] = frames[i].localScale;
            }
            Transform floor = backdrop.transform.Find("Floor");
            floorSurfaces = new[] { floor.Find("Floor"), floor.Find("EdgeStrip_L"), floor.Find("EdgeStrip_R") };
            floorPositions = new Vector3[floorSurfaces.Length];
            for (int i = 0; i < floorSurfaces.Length; i++) floorPositions[i] = floorSurfaces[i].localPosition;
            BuildInterface();
            menu.PlayRequested += Begin;
            ControllerService.Instance.Disconnected += LostController;
            look.StudyControlled = true;
            ResetToMenu();
        }

        public void Begin()
        {
            if (!isActiveAndEnabled || root == null || started || titlePanel.resolvedStyle.display == DisplayStyle.None) return;
            // Snapshot layout while it is untransformed. The existing title texture and captions
            // stay in the same UI renderer, avoiding a world/UI brightness or pixel-size jump.
            depthElements.Clear();
            foreach (var name in new[] { "game-title", "play-button", "options-button", "help-button", "quit-button" })
            {
                VisualElement element = root.Q(name);
                Translate translation = element.resolvedStyle.translate;
                depthElements.Add(new DepthElement { Element = element, Bounds = UntransformedBounds(element),
                    Translation = new Vector2(translation.x.value, translation.y.value),
                    Depth = name == "game-title" ? 26f : 17f + depthElements.Count * 3f });
                element.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            }
            started = true;
            menu.Suspended = true;
            controls.style.display = DisplayStyle.None;
            clock = 0f;
            foreach (var construct in constructs) construct.ResetPresentationClock();
            look.CameraSuppliesMotion = true;
            backdrop.SetPresentationMotion(true, cameraStart.z);
        }

        public void Select(DepartureTreatment next)
        {
            ResetToMenu();
            treatment = next;
            pendingStartFrame = Time.frameCount + 2; // Let restored styles/submenu layout resolve.
        }

        public void ResetToMenu()
        {
            if (root == null) return;
            started = paused = transferred = false;
            clock = idleClock = forcedCover = 0f;
            coveredFrame = -1;
            pendingStartFrame = -1;
            pauseReason = null;
            Current = default;
            world.position = worldStart;
            view.transform.SetPositionAndRotation(cameraStart, rotationStart);
            view.fieldOfView = fovStart;
            foreach (var entry in depthElements)
            {
                // Resolve the restored transform without interpolating back from departure.
                // Native hover/focus transitions can resume once idle styles have settled.
                entry.Element.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
                entry.Element.style.translate = StyleKeyword.Null;
                entry.Element.style.scale = StyleKeyword.Null;
                entry.Element.style.visibility = StyleKeyword.Null;
                entry.Element.style.opacity = StyleKeyword.Null;
                entry.Element.schedule.Execute(() =>
                {
                    if (!started && pendingStartFrame < 0)
                        entry.Element.style.transitionDuration = StyleKeyword.Null;
                }).ExecuteLater(100);
            }
            depthElements.Clear();
            menu.Suspended = false;
            menu.ReturnToTitle();
            titlePanel.pickingMode = PickingMode.Position;
            look.DepartureAmount = 0f;
            look.CameraSuppliesMotion = false;
            look.PresentationPaused = false;
            look.RebuildAtmosphere();
            backdrop.SetPresentationMotion(true, cameraStart.z);
            backdrop.SetPresentationMotion(false);
            // Keep the same nearest-frame phase at arrival while giving return its own shorter approach.
            float firstFrameAhead = Mathf.Repeat(framePositions[0].z - MenuDepartureTimeline.FullDistance, 22f);
            arrival.localPosition = Vector3.forward * (returning
                ? MenuDepartureTimeline.ReturnDistance + firstFrameAhead - framePositions[0].z : 0f);
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i].localPosition = framePositions[i];
                frames[i].localScale = frameScales[i];
                frames[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < floorSurfaces.Length; i++) floorSurfaces[i].localPosition = floorPositions[i];
            foreach (var construct in constructs) construct.ResetPresentationClock();
            mask.style.opacity = 0f;
            controls.style.display = DisplayStyle.Flex;
            root.Q<Button>("play-button").Focus();
        }

        private void Update()
        {
            if (root == null) return;
            if (pendingStartFrame >= 0 && Time.frameCount >= pendingStartFrame)
            {
                pendingStartFrame = -1;
                Begin();
            }
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.f1Key.wasPressedThisFrame) Select(DepartureTreatment.FlyThrough);
                else if (keys.f2Key.wasPressedThisFrame) Select(DepartureTreatment.SignalTransfer);
                else if (keys.f3Key.wasPressedThisFrame) ChangeDuration();
                else if (keys.f4Key.wasPressedThisFrame) ToggleReturn();
                else if (keys.f5Key.wasPressedThisFrame) ToggleReduced();
                else if (keys.backspaceKey.wasPressedThisFrame) ResetToMenu();
                else if (keys.escapeKey.wasPressedThisFrame && started) TogglePause();
            }
            ControllerState device = ControllerService.Instance.State;
            bool fire = device.Connected && device.Fire;
            bool menuPressed = device.Connected && device.StickButtonPressed;
            if (device.Connected && !fire) resumeNeedsRelease = false;
            if (fire && !physicalFire)
            {
                if (paused && !resumeNeedsRelease) TogglePause();
                else if (!started && root.focusController.focusedElement == root.Q<Button>("play-button")) Begin();
            }
            // The service reports a held MENU as a new edge on its first reconnect packet.
            if (menuPressed && !physicalMenu && controllerConnected && started) TogglePause();
            physicalFire = fire; physicalMenu = menuPressed;
            controllerConnected = device.Connected;

            float dt = paused ? 0f : Time.unscaledDeltaTime;
            if (!started)
            {
                idleClock += dt;
                foreach (var construct in constructs) construct.SetPresentationTime(idleClock, dt);
                return;
            }
            clock += dt;
            Current = MenuDepartureTimeline.Sample(clock, Duration, returning, reduced, treatment);
            bool needsTransfer = reduced || treatment == DepartureTreatment.SignalTransfer;
            forcedCover = 0f;
            if (needsTransfer && !transferred && clock >= Duration * (reduced ? 0.4f : 0.52f))
            {
                // At least one completely covered rendered frame must precede the origin change.
                // A long frame cannot skip the mask window and reveal the jump.
                if (coveredFrame < 0) coveredFrame = Time.frameCount;
                forcedCover = 1f;
                if (!paused && Time.frameCount > coveredFrame)
                {
                    transferred = true;
                    world.position = worldStart + Vector3.right * 1000f;
                    view.transform.position += Vector3.right * 1000f;
                    look.RebuildAtmosphere();
                }
                else clock = Duration * (reduced ? 0.4f : 0.52f);
            }
            foreach (var construct in constructs) construct.SetPresentationTime(clock, dt);
            ApplyView();
        }

        private void ApplyView()
        {
            Vector3 offset = world.position - worldStart;
            float progress = Mathf.Clamp01(clock / Duration);
            float pitchBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.7f));
            Vector3 position = cameraStart + offset + Vector3.forward * Current.Distance;
            position.y = reduced ? cameraStart.y : Mathf.Lerp(cameraStart.y, 5f, pitchBlend);
            if (reduced && transferred) position.y = 5f;
            view.transform.SetPositionAndRotation(position, reduced && !transferred ? rotationStart
                : Quaternion.Slerp(rotationStart, Quaternion.identity, reduced ? 1f : pitchBlend));
            view.fieldOfView = fovStart + Current.FovSurge;
            backdrop.SetPresentationMotion(true, position.z);
            look.DepartureAmount = reduced ? (transferred ? 1f : 0f) : Mathf.Clamp01(Current.Distance / 24f);
            look.PresentationPaused = paused;

            for (int i = 0; i < floorSurfaces.Length; i++)
            {
                Vector3 p = floorPositions[i];
                p.z += Mathf.Max(0f, Current.Distance - 250f);
                floorSurfaces[i].localPosition = p;
            }
            for (int i = 0; i < frames.Length; i++)
            {
                Vector3 p = framePositions[i];
                float canonicalZ = p.z + arrival.localPosition.z;
                float cycle = frames.Length * 22f;
                if (canonicalZ < Current.Distance - 20f)
                    p.z += Mathf.Ceil((Current.Distance - 20f - canonicalZ) / cycle) * cycle;
                frames[i].localPosition = p;
                float reveal = reduced ? (transferred ? 1f : 0f)
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.20f - i * 0.025f) / 0.25f));
                frames[i].gameObject.SetActive(reveal > 0f);
                // Reused frame geometry grows into a readable destination, in the same order for A/B.
                frames[i].localScale = frameScales[i] * Mathf.Lerp(0.15f, 1f, reveal);
            }
        }

        private void LateUpdate()
        {
            if (root == null) return;
            if (started) ApplyDepth();
            float cover = Mathf.Max(forcedCover, Current.Occlusion);
            mask.style.opacity = started ? cover : 0f;
            string name = treatment == DepartureTreatment.FlyThrough ? "A / CONTINUOUS FLIGHT" : "B / SIGNAL TRANSFER";
            heading.text = "DEPARTURE STUDY   " + name;
            string mode = reduced ? "QUIET TRANSFER" : returning ? "RETURN PREVIEW" : "FIRST ENTRY";
            string state = paused ? "PAUSED — " + pauseReason : !started ? "READY" : clock < Duration ? "DEPARTING"
                : clock < Duration + 3f ? "ARRIVAL — KEEPING MOTION" : "RAIL-START REFERENCE · CAMERA STUDY";
            detail.text = $"{mode} · {Duration:0}s · {state}\nF1 / F2 compare · F3 duration · F4 return · F5 reduced\nESC / MENU pause · BACKSPACE menu";
            if (started)
                ControllerService.Instance.Display.Show(LcdGlyph.Center(paused ? "PAUSED" : "GRID TRANSIT"),
                    LcdGlyph.Center(paused ? "FIRE TO RESUME" : clock >= Duration ? "ARRIVAL STUDY" : "VIEW TRANSFER"));
        }

        private void ApplyDepth()
        {
            Vector2 center = root.worldBound.center;
            float fovRatio = Mathf.Tan(fovStart * Mathf.Deg2Rad * 0.5f) / Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
            foreach (var entry in depthElements)
            {
                entry.Bounds = UntransformedBounds(entry.Element); // Layout adapts to resize, not our visual scale.
                if (reduced)
                {
                    entry.Element.style.visibility = transferred ? Visibility.Hidden : Visibility.Visible;
                    continue;
                }
                float advance = Mathf.Max(0f, Current.Distance - 1f);
                float remaining = entry.Depth - advance;
                if (remaining < 1f)
                {
                    entry.Element.style.visibility = Visibility.Hidden;
                    continue;
                }
                float scale = entry.Depth / remaining * fovRatio;
                Vector2 shift = (entry.Bounds.center + entry.Translation - center) * (scale - 1f) + entry.Translation;
                entry.Element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                entry.Element.style.translate = new Translate(shift.x, shift.y);
            }
        }

        private static Rect UntransformedBounds(VisualElement element) =>
            new Rect(element.parent.LocalToWorld(element.layout.position), element.layout.size);

        public void ChangeDuration() { ResetToMenu(); duration = duration < 9f ? 10f : duration < 11f ? 12f : 8f; }
        public void ToggleReturn() { ResetToMenu(); returning = !returning; ResetToMenu(); }
        public void ToggleReduced() { ResetToMenu(); reduced = !reduced; }
        public void TogglePause()
        {
            if (!started) return;
            paused = !paused;
            pauseReason = paused ? "ESC / FIRE TO RESUME" : null;
            look.PresentationPaused = paused;
        }
        private void LostController()
        {
            if (!started) return;
            paused = true;
            resumeNeedsRelease = true;
            pauseReason = "COCKPIT OFFLINE · ESC TO RESUME";
            look.PresentationPaused = true;
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && started && !Application.runInBackground)
            {
                paused = true;
                pauseReason = "FOCUS LOST · ESC TO RESUME";
            }
        }

        private void BuildInterface()
        {
            mask = new VisualElement { name = "departure-cover", pickingMode = PickingMode.Ignore };
            mask.style.position = Position.Absolute;
            mask.style.left = mask.style.right = mask.style.top = mask.style.bottom = 0f;
            mask.style.backgroundColor = AxiomPalette.Void;
            mask.style.opacity = 0f;
            root.Add(mask);
            for (int i = 0; i < 3; i++)
            {
                var trace = new VisualElement { pickingMode = PickingMode.Ignore };
                trace.style.position = Position.Absolute;
                trace.style.top = Length.Percent(32f + i * 17f);
                trace.style.left = Length.Percent(8f + i * 11f);
                trace.style.width = Length.Percent(72f - i * 12f);
                trace.style.height = 2f;
                trace.style.backgroundColor = AxiomPalette.Cyan;
                mask.Add(trace);
            }
            root.Q("frame")?.BringToFront();
            root.Q("menu-footer")?.BringToFront();
            readout = new VisualElement { name = "departure-readout", pickingMode = PickingMode.Ignore };
            readout.AddToClassList("screen"); // Inherit the original menu's themed buttons and type.
            readout.style.position = Position.Absolute;
            readout.style.left = StyleKeyword.Auto; readout.style.bottom = StyleKeyword.Auto;
            readout.style.alignItems = Align.FlexEnd;
            readout.style.justifyContent = Justify.FlexStart;
            readout.style.right = 56f; readout.style.top = 52f;
            readout.style.maxWidth = Length.Percent(40f);
            heading = Label(readout, 14, AxiomPalette.Light);
            detail = Label(readout, 12, AxiomPalette.Text);
            root.Add(readout);
            controls = new VisualElement { name = "departure-controls" };
            readout.Add(controls);
            AddButton("A · FLY THROUGH", () => Select(DepartureTreatment.FlyThrough));
            AddButton("B · SIGNAL TRANSFER", () => Select(DepartureTreatment.SignalTransfer));
            AddButton("CHANGE DURATION", ChangeDuration);
            AddButton("RETURN PREVIEW", ToggleReturn);
            AddButton("REDUCED ANIMATION", ToggleReduced);
        }

        private void AddButton(string caption, Action action)
        {
            var button = new SlantButton { text = caption };
            button.AddToClassList("menu-button");
            button.style.width = 300f; button.style.height = 32f;
            button.style.minWidth = 0f;
            button.style.fontSize = 12f; button.style.letterSpacing = 1f;
            button.style.marginTop = 4f; button.style.marginBottom = 0f;
            button.clicked += action;
            controls.Add(button);
        }
        private static Label Label(VisualElement parent, int size, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("micro");
            label.style.color = color; label.style.fontSize = size;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }
        private void OnDestroy()
        {
            if (menu != null) menu.PlayRequested -= Begin;
            if (ControllerService.Current != null) ControllerService.Current.Disconnected -= LostController;
            mask?.RemoveFromHierarchy(); readout?.RemoveFromHierarchy();
        }

        private void OnEnable()
        {
            if (root == null) return; // First initialization belongs to Start, after MainMenu.OnEnable.
            menu.PlayRequested += Begin;
            ControllerService.Instance.Disconnected += LostController;
            look.StudyControlled = true;
            readout.style.display = DisplayStyle.Flex;
            ResetToMenu();
        }

        private void OnDisable()
        {
            if (root == null) return;
            if (menu != null) menu.PlayRequested -= Begin;
            if (ControllerService.Current != null) ControllerService.Current.Disconnected -= LostController;
            if (!Application.isPlaying) return;
            ResetToMenu();
            look.StudyControlled = false;
            backdrop.SetPresentationMotion(false);
            foreach (var construct in constructs) construct.ReleasePresentationClock();
            readout.style.display = DisplayStyle.None;
        }
    }
}

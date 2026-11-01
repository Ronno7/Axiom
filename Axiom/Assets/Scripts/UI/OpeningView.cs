using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Entry UI and route-line handoff. Observes OpeningFlow; all buttons call its actions.</summary>
    [DefaultExecutionOrder(150), RequireComponent(typeof(UIDocument))]
    public sealed class OpeningView : MonoBehaviour
    {
        [SerializeField] private OpeningFlow flow;
        [SerializeField] private OpeningPresentation presentation;
        [SerializeField] private VisualTreeAsset layout;
        private VisualElement root, container, overlay, choices, hud, address, instruments, pausePanel, finishPanel, trace;
        private Label headline, help, speed, arm, score, hints, console, weapons, menuArm, pauseText, pauseArm, finalScore;
        private OpeningStage previousStage = (OpeningStage)(-1);
        private bool wasPaused;
        private Rect buttonRect;
        private float stageEntered;
        private Vector2 traceFrom, traceTo;
        private float instrumentProgress;

        private void Bind()
        {
            VisualElement current = GetComponent<UIDocument>().rootVisualElement;
            if (current == root && overlay != null && overlay.panel != null) return;
            container?.RemoveFromHierarchy();
            root = current;
            container = layout.Instantiate();
            container.style.position = Position.Absolute;
            container.style.left = container.style.right = container.style.top = container.style.bottom = 0;
            container.pickingMode = PickingMode.Ignore;
            root.Add(container);
            overlay = container.Q("opening-overlay");
            choices = container.Q("opening-choices");
            hud = container.Q("opening-hud");
            address = container.Q("opening-address");
            instruments = container.Q("opening-instruments");
            instruments.generateVisualContent += DrawInstrumentFrame;
            pausePanel = container.Q("opening-pause");
            finishPanel = container.Q("opening-finish");
            headline = container.Q<Label>("address-text"); help = container.Q<Label>("address-help");
            speed = container.Q<Label>("opening-speed"); arm = container.Q<Label>("opening-arm");
            score = container.Q<Label>("opening-score"); console = container.Q<Label>("opening-console");
            hints = container.Q<Label>("opening-hints"); weapons = container.Q<Label>("opening-weapons");
            menuArm = container.Q<Label>("menu-arm"); pauseText = container.Q<Label>("opening-pause-text");
            pauseArm = container.Q<Label>("opening-pause-arm"); finalScore = container.Q<Label>("opening-final-score");
            container.Q<Button>("replay-training").clicked += () => flow.Begin(true);
            container.Q<Button>("scored-entry").clicked += () => flow.Begin(false);
            container.Q<Button>("opening-resume").clicked += flow.Resume;
            container.Q<Button>("opening-keyboard").clicked += flow.KeyboardTakeover;
            container.Q<Button>("opening-controller").clicked += flow.UseController;
            container.Q<Button>("opening-back").clicked += flow.ReturnToMenu;
            container.Q<Button>("opening-finish-menu").clicked += flow.ReturnToMenu;
            container.Q<Button>("opening-again").clicked += () => flow.Begin(false);
            var watched = container.Q<Toggle>("watched-reveal"); watched.SetValueWithoutNotify(flow.WatchedReveal);
            watched.RegisterValueChangedCallback(e => flow.WatchedReveal = e.newValue);
            var reduced = container.Q<Toggle>("reduce-motion"); reduced.SetValueWithoutNotify(flow.ReducedAnimation);
            reduced.RegisterValueChangedCallback(e => flow.ReducedAnimation = e.newValue);
            trace = new VisualElement { name = "route-trace", pickingMode = PickingMode.Ignore };
            trace.style.position = Position.Absolute;
            trace.style.left = trace.style.right = trace.style.top = trace.style.bottom = 0;
            trace.generateVisualContent += DrawTrace;
            overlay.Add(trace);
        }

        private void LateUpdate()
        {
            Bind();
            var stage = flow.Stage;
            bool menu = stage == OpeningStage.Menu;
            bool arrival = stage == OpeningStage.Arrival;
            if (stage != previousStage)
            {
                if (arrival) buttonRect = root.Q("play-button").worldBound;
                stageEntered = Time.time;
                if (stage == OpeningStage.Finished) container.Q<Button>("opening-again").Focus();
                if (menu) root.Q<Button>("play-button").Focus();
                previousStage = stage;
            }
            bool titleVisible = root.Q("title-panel").resolvedStyle.display != DisplayStyle.None;
            Show(choices, menu && titleVisible);
            Show(hud, !menu && stage != OpeningStage.Finished);
            Show(pausePanel, flow.Paused);
            Show(finishPanel, stage == OpeningStage.Finished);
            if (flow.Paused && !wasPaused) container.Q<Button>("opening-resume").Focus();
            wasPaused = flow.Paused;
            var screen = root.Q("screen");
            Show(screen, menu || arrival);
            screen.SetEnabled(menu);
            float fade = arrival ? 1 - Mathf.Clamp01(flow.ArrivalProgress * 2.5f) : menu ? 1 : 0;
            screen.style.opacity = fade;
            root.Q("game-title").style.scale = new Scale(Vector3.one * (arrival ? Mathf.Lerp(1, 0.86f, flow.ArrivalProgress) : 1));
            root.Q("scrim").style.opacity = fade;
            // Keep the original frame and system/link footer through flight. MenuLook owns their
            // visibility; only the menu navigation hints and flight spacing change here.
            root.EnableInClassList("opening-in-flight", !menu);
            container.EnableInClassList("opening-in-flight", !menu);
            Show(trace, arrival && !flow.ReducedAnimation);
            if (arrival) UpdateTrace();

            string armStatus = flow.Authority.Armed ? "WEAPONS ARMED" : "WEAPONS SAFE";
            string armControl = flow.Authority.HasPhysicalSwitch ? "MASTER ARM" : "T / MASTER ARM";
            menuArm.text = armStatus + "  /  " + armControl;
            pauseArm.text = armStatus + "  /  " + (flow.Authority.KeyboardOnly ? "KEYBOARD" : "CONTROLLER AUTO");
            pauseText.text = flow.PauseNotice;
            container.Q<Button>("opening-controller").SetEnabled(ControllerService.Instance.State.Connected);
            finalScore.text = $"{flow.Score:0000} POINTS     {flow.GatesHit}/8 GATES";
            Show(instruments, !arrival);
            speed.text = $"{flow.Flight.CurrentSpeed:00} M/S";
            arm.text = flow.Authority.WeaponsAvailable ? armStatus : "WEAPONS / STANDBY";
            score.text = stage == OpeningStage.Course ? $"SCORE {flow.Score:0000}     GATES {flow.GatesHit}/8" : "COMBATANT / COMMISSIONING";
            var emp = flow.Flight.GetComponent<EmpWeapon>();
            weapons.text = flow.Authority.WeaponsAvailable
                ? $"GUN / SPACE   LOCK / SHIFT\nEMP / R   {(emp.State.Ready ? "READY" : "SPENT")}" : "";
            // Essential values are present immediately; the decorative border grows around them.
            instrumentProgress = flow.ReducedAnimation ? 1 : Mathf.Clamp01((Time.time - stageEntered) * 2);
            instruments.MarkDirtyRepaint();
            var track = container.Q("speed-track");
            Show(track, stage >= OpeningStage.Throttle);
            container.Q("speed-fill").style.width = Length.Percent(100 * Mathf.InverseLerp(flow.Flight.MinSpeed,
                flow.Flight.MaxSpeed, flow.Flight.CurrentSpeed));
            hints.text = flow.Tutorial && stage != OpeningStage.Course
                ? "ESC / MENU   PAUSE       TAB   SKIP TRAINING" : "ESC / MENU   PAUSE       GATES +100   TARGETS +50";
            SetAddress(stage, armControl);
            if (Time.unscaledTime < flow.RefusalUntil)
            {
                headline.text = "WEAPONS SAFE";
                help.text = "Enable " + armControl + " to fire. Flight remains active.";
            }
            Show(address, !flow.Paused && (stage != OpeningStage.Course || Time.time - stageEntered < 3 ||
                !flow.Authority.Armed || Time.unscaledTime < flow.RefusalUntil));
            bool telemetry = arrival && !flow.ReducedAnimation;
            Show(console, telemetry);
            if (telemetry)
            {
                int sequence = Mathf.FloorToInt(flow.ArrivalTime * 18);
                console.text = $"ROUTE / A-01\nGEOMETRY {sequence:0000}\nCORRIDOR / RESERVED\nTRAFFIC / CONTINUING";
            }
            if (!menu)
                ControllerService.Instance.Display.Show(LcdGlyph.Center(flow.Paused ? "PAUSED" : armStatus),
                    LcdGlyph.Center(stage == OpeningStage.Course ? "SCORE " + flow.Score : headline.text));
        }

        private void SetAddress(OpeningStage stage, string armControl)
        {
            switch (stage)
            {
                case OpeningStage.Arrival: headline.text = "COMBATANT REGISTERED"; help.text = "ROUTE A-01 / ASSIGNED"; break;
                case OpeningStage.Steering: headline.text = "FLIGHT CONTROL ACTIVE"; help.text = "Move the stick or use WASD / arrows. The corridor is clear."; break;
                case OpeningStage.Throttle: headline.text = "THROTTLE CONTROL"; help.text = "Change speed with the throttle or Q / E."; break;
                case OpeningStage.Commissioning: headline.text = flow.Authority.Armed ? "WEAPONS AUTHORITY ACCEPTED" : "WEAPONS AUTHORITY REQUIRED"; help.text = "Enable " + armControl + ". SAFE inhibits weapons while you keep flying."; break;
                case OpeningStage.Guns: headline.text = "TARGET PRACTICE"; help.text = "Hold FIRE or Space to destroy the orange target. SAFE remains available."; break;
                case OpeningStage.Course: headline.text = flow.Authority.Armed ? "ROUTE A-01 / ACTIVE" : "WEAPONS SAFE"; help.text = flow.Authority.Armed ? "Fly through the cyan gates. Orange targets add points." : "Enable " + armControl + " to fire. Gates remain scoreable."; break;
            }
        }

        private void UpdateTrace()
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(flow.ArrivalProgress * 1.7f));
            Vector2 near = Project(presentation.RoutePoint(-4));
            Vector2 far = Project(presentation.RoutePoint(150));
            traceFrom = Vector2.Lerp(new Vector2(buttonRect.xMin, buttonRect.yMax), near, t);
            traceTo = Vector2.Lerp(new Vector2(buttonRect.xMax, buttonRect.yMax), far, t);
            trace.style.opacity = 1 - Mathf.Clamp01((flow.ArrivalProgress - 0.8f) * 5);
            trace.MarkDirtyRepaint();
        }

        private Vector2 Project(Vector3 point)
        {
            Vector3 viewport = presentation.View.WorldToViewportPoint(point);
            return new Vector2(viewport.x * root.resolvedStyle.width, (1 - viewport.y) * root.resolvedStyle.height);
        }

        private void DrawTrace(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.strokeColor = AxiomPalette.Light;
            painter.lineWidth = 2;
            painter.BeginPath(); painter.MoveTo(traceFrom); painter.LineTo(traceTo); painter.Stroke();
        }

        private void DrawInstrumentFrame(MeshGenerationContext context)
        {
            float width = instruments.contentRect.width + instruments.resolvedStyle.paddingLeft + instruments.resolvedStyle.paddingRight;
            float height = instruments.layout.height;
            if (width <= 0 || height <= 0) return;
            var corners = new[]{new Vector2(1,1), new Vector2(width-1,1), new Vector2(width-1,height-1),
                new Vector2(1,height-1), new Vector2(1,1)};
            float remaining = instrumentProgress * (2 * width + 2 * height - 8);
            var painter = context.painter2D;
            painter.strokeColor = AxiomPalette.WithAlpha(AxiomPalette.Cyan, 0.8f); painter.lineWidth = 1;
            painter.BeginPath(); painter.MoveTo(corners[0]);
            for (int i = 0; i < 4 && remaining > 0; i++)
            {
                float distance = Vector2.Distance(corners[i], corners[i + 1]);
                painter.LineTo(Vector2.Lerp(corners[i], corners[i + 1], Mathf.Clamp01(remaining / distance)));
                remaining -= distance;
            }
            painter.Stroke();
        }

        private static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}

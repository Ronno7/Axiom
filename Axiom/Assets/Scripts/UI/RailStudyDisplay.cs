using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Small study readout added to the established HUD; LevelUi keeps every menu and LCD role.</summary>
    [DefaultExecutionOrder(120), RequireComponent(typeof(UIDocument))]
    public sealed class RailStudyDisplay : MonoBehaviour
    {
        [SerializeField] private RailHandlingStudy study;
        private VisualElement panel, pauseChoices;
        private Label title, progress, controls;
        private ArenaFlight flight;
        private MasterArmInput authority;

        private void Start()
        {
            flight = study.GetComponent<ArenaFlight>();
            authority = study.GetComponent<MasterArmInput>();
        }

        private void LateUpdate()
        {
            if (study == null || flight == null) return;
            var root = GetComponent<UIDocument>().rootVisualElement;
            var hud = root?.Q("hud");
            if (hud == null) return;
            if (panel == null || panel.parent != hud) Build(root, hud);
            string handling = flight.FullRotation ? "ROLL & PITCH" : "BANK & TURN";
            string candidate = study.Candidate == RailTravelMode.NoseLed ? "A / NOSE-LED" : "B / COURSE-DRIVEN";
            title.text = candidate + "   ·   " + handling;
            progress.text = (study.Released ? "ARENA" : "RAIL") + $"   {flight.Velocity.magnitude:0} m/s   HOOPS {study.HoopsHit}/{study.HoopsTotal}   SCORE {study.Score}"
                + (authority.Armed ? "   ARM" : "   SAFE — T / SWITCH");
            controls.text = "F1 A · F2 B · M HANDLING · BACKSPACE RETRY\nEach choice restarts. Q/E throttle · T ARM · F8 report";
            if (study.ReleaseComparisonInvalid)
                controls.text += "\nEXIT OUTSIDE COMPARISON AREA — retry through the marked passage";
        }

        private void Build(VisualElement root, VisualElement hud)
        {
            panel?.RemoveFromHierarchy();
            pauseChoices?.RemoveFromHierarchy();
            panel = new VisualElement { name = "rail-study", pickingMode = PickingMode.Ignore };
            panel.style.position = Position.Absolute;
            panel.style.left = 56; panel.style.top = 94;
            panel.style.maxWidth = Length.Percent(70);
            hud.Add(panel);
            title = AddLabel(panel, 16, AxiomPalette.Light);
            progress = AddLabel(panel, 14, AxiomPalette.Cyan);
            controls = AddLabel(panel, 12, AxiomPalette.Text);
            var pause = root.Q("pause-panel");
            if (pause == null) return;
            pauseChoices = new VisualElement { name = "study-choices" };
            pause.Add(pauseChoices);
            AddButton("TRY A · NOSE-LED", () => study.Restart(RailTravelMode.NoseLed, flight.FullRotation));
            AddButton("TRY B · COURSE-DRIVEN", () => study.Restart(RailTravelMode.CourseDriven, flight.FullRotation));
            AddButton("SWITCH HANDLING + RESTART", () => study.Restart(study.Candidate, !flight.FullRotation));
        }

        private void AddButton(string text, System.Action clicked)
        {
            var button = new SlantButton { text = text };
            button.AddToClassList("menu-button");
            button.style.height = 44;
            button.style.fontSize = 18;
            button.style.letterSpacing = 2;
            button.clicked += clicked;
            pauseChoices.Add(button);
        }

        private static Label AddLabel(VisualElement parent, int size, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("hud-readout");
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        private void OnDisable()
        {
            panel?.RemoveFromHierarchy();
            pauseChoices?.RemoveFromHierarchy();
        }
    }
}

using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>Missile bank, acquisition brackets and cockpit cues. Reads the weapon; never changes it.</summary>
    [DefaultExecutionOrder(110)] // After the chase camera and LevelLook's panel routing.
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class MissileDisplay : MonoBehaviour
    {
        [SerializeField] private LockOnWeapon weapon;
        [Tooltip("Disable when an existing level HUD owns the cockpit LCD. Missile tones still play.")]
        [SerializeField] private bool writeCockpit = true;
        [SerializeField, Min(0f)] private float bottomMargin = 24f;
        private static readonly Color Cyan = AxiomPalette.Cyan;
        private static readonly Color Ready = AxiomPalette.Light;
        private readonly VisualElement[] markers = new VisualElement[9];
        private readonly Label[] numbers = new Label[9];
        private readonly VisualElement[] pips = new VisualElement[8];
        private UIDocument document;
        private VisualElement overlay, panel, edge;
        private Label bank, prompt, rangeHint, empLabel;
        private EmpWeapon emp;
        private Camera view;
        private int lastLocks, lastFired, toneStep = -1;

        /// <summary>0–1 while an entry draws the panel in: its edge draws up, the bank types in, the sockets
        /// appear one by one and then show their real contents, then the prompts type. Shows the idle state,
        /// no lock markers and no cockpit cues until 1.</summary>
        public float Assembly { get; set; } = 1f;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            view = Camera.main;
            emp = weapon != null ? weapon.GetComponent<EmpWeapon>() : null;
        }

        private void Build(VisualElement root)
        {
            overlay?.RemoveFromHierarchy();
            overlay = new VisualElement { name = "missile-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
            root.Add(overlay);
            panel = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.style.position = Position.Absolute;
            panel.style.right = 24;
            panel.style.bottom = bottomMargin;
            panel.style.width = 280;
            panel.style.paddingLeft = panel.style.paddingRight = 14;
            panel.style.paddingTop = panel.style.paddingBottom = 10;
            panel.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f);
            panel.style.borderLeftWidth = 2;
            panel.style.borderLeftColor = Cyan;
            overlay.Add(panel);
            edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.style.position = Position.Absolute;
            edge.style.left = -2;
            edge.style.top = edge.style.bottom = 0;
            edge.style.width = 2;
            edge.style.backgroundColor = Cyan;
            edge.style.transformOrigin = new TransformOrigin(0f, Length.Percent(100f));
            edge.style.display = DisplayStyle.None;
            panel.Add(edge);
            bank = new Label { pickingMode = PickingMode.Ignore };
            bank.style.fontSize = 17;
            bank.style.color = Ready;
            bank.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(bank);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = row.style.marginBottom = 8;
            panel.Add(row);
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                pips[i].style.width = 25;
                pips[i].style.height = 5;
                pips[i].style.marginRight = 5;
                row.Add(pips[i]);
            }
            prompt = new Label { pickingMode = PickingMode.Ignore };
            prompt.style.fontSize = 12;
            prompt.style.color = Cyan;
            panel.Add(prompt);
            rangeHint = new Label { pickingMode = PickingMode.Ignore };
            rangeHint.style.fontSize = 12;
            rangeHint.style.color = Cyan;
            rangeHint.style.marginTop = 5;
            panel.Add(rangeHint);
            empLabel = new Label { pickingMode = PickingMode.Ignore };
            empLabel.style.fontSize = 15;
            empLabel.style.marginTop = 12;
            empLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(empLabel);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.style.position = Position.Absolute;
                marker.style.width = marker.style.height = 48;
                for (int c = 0; c < 4; c++)
                {
                    var corner = new VisualElement { pickingMode = PickingMode.Ignore };
                    corner.style.position = Position.Absolute;
                    corner.style.width = corner.style.height = 12;
                    if ((c & 1) == 0) { corner.style.left = 0; corner.style.borderLeftWidth = 2; }
                    else { corner.style.right = 0; corner.style.borderRightWidth = 2; }
                    if ((c & 2) == 0) { corner.style.top = 0; corner.style.borderTopWidth = 2; }
                    else { corner.style.bottom = 0; corner.style.borderBottomWidth = 2; }
                    marker.Add(corner);
                }
                numbers[i] = new Label { pickingMode = PickingMode.Ignore };
                numbers[i].style.position = Position.Absolute;
                numbers[i].style.top = 50;
                numbers[i].style.width = 110;
                numbers[i].style.fontSize = 11;
                marker.Add(numbers[i]);
                markers[i] = marker;
                overlay.Add(marker);
            }
        }

        private void LateUpdate()
        {
            if (weapon == null || weapon.State == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
            if (overlay == null || overlay.parent != root) Build(root);
            MissileLockState state = weapon.State;
            bank.text = $"MISSILES  {state.Ammo}/{state.Capacity}";
            rangeHint.text = weapon.CloseTargetInCone ? "CLOSE TARGET — USE GUNS"
                : $"LOCK {weapon.MinimumRange:0}–{weapon.MaximumRange:0} m / GUNS CLOSE";
            empLabel.style.display = emp != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (emp != null)
            {
                empLabel.text = emp.Pulsing ? "EMP DISCHARGED" : emp.State.Ready
                    ? "EMP READY  /  DOME or R" : "EMP SPENT  /  ONE PER LIFE";
                empLabel.style.color = emp.State.Ready ? AxiomPalette.Light : AxiomPalette.Muted;
            }
            prompt.text = !weapon.Available ? "RESTARTING" : Time.timeScale <= 0f ? "PAUSED"
                : state.IsPainting ? state.Locks.Count > 0 ? $"RELEASE TO FIRE  /  {state.Locks.Count} LOCKED"
                    : state.Ammo == 0 ? "MISSILES RECHARGING" : weapon.CloseTargetInCone ? "TOO CLOSE TO LOCK" : "PAINT TARGETS"
                : ControllerService.Current != null && ControllerService.Current.State.HasLock
                    ? "HOLD LOCK BUTTON / LEFT SHIFT" : "HOLD LEFT SHIFT TO LOCK";
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].style.display = i < state.Capacity ? DisplayStyle.Flex : DisplayStyle.None;
                pips[i].style.backgroundColor = i < state.Locks.Count ? Ready : i < state.Ammo ? Cyan
                    : AxiomPalette.WithAlpha(Cyan, i == state.Ammo ? 0.12f + state.RefillProgress * 0.4f : 0.12f);
            }
            for (int i = 0; i < markers.Length; i++) markers[i].style.display = DisplayStyle.None;
            if (Assembly < 1f)
            {
                ApplyAssembly(state);
                return;
            }
            if (edge.style.display != DisplayStyle.None) ApplyAssembly(state);
            if (weapon.Available && Time.timeScale > 0f)
            {
                for (int i = 0; i < state.Locks.Count; i++)
                    ShowMarker(i, weapon.Target(state.Locks[i]), Ready, "LOCK " + (i + 1), root);
                ShowMarker(8, weapon.Target(state.CandidateId), Color.Lerp(Cyan * 0.55f, Ready, state.Progress),
                    "ACQUIRE " + Mathf.RoundToInt(state.Progress * 100f) + "%", root);
            }
            Cockpit(state);
        }

        private void ApplyAssembly(MissileLockState state)
        {
            float a = Mathf.Clamp01(Assembly);
            bool done = a >= 1f;
            edge.style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
            edge.style.scale = new Scale(new Vector3(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.25f, a)), 1f));
            panel.style.borderLeftWidth = done ? 2 : 0;
            panel.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f * Mathf.InverseLerp(0.1f, 0.4f, a));
            if (done)
            {
                foreach (VisualElement pip in pips) pip.style.visibility = StyleKeyword.Null;
                return;
            }
            // The idle prompt, not RESTARTING: weapons are only released at control.
            prompt.text = ControllerService.Current != null && ControllerService.Current.State.HasLock
                ? "HOLD LOCK BUTTON / LEFT SHIFT" : "HOLD LEFT SHIFT TO LOCK";
            bank.text = InstrumentBuild.Reveal(bank.text, Mathf.InverseLerp(0.15f, 0.4f, a));
            for (int i = 0; i < pips.Length; i++)
            {
                if (i >= state.Capacity) continue;
                bool socket = a >= 0.4f + i * 0.06f;
                pips[i].style.visibility = socket ? Visibility.Visible : Visibility.Hidden;
                if (a < 0.7f) pips[i].style.backgroundColor = AxiomPalette.WithAlpha(Cyan, 0.12f);
            }
            prompt.text = InstrumentBuild.Reveal(prompt.text, Mathf.InverseLerp(0.7f, 0.85f, a));
            rangeHint.text = InstrumentBuild.Reveal(rangeHint.text, Mathf.InverseLerp(0.75f, 0.9f, a));
            empLabel.text = InstrumentBuild.Reveal(empLabel.text, Mathf.InverseLerp(0.8f, 0.97f, a));
        }

        private void ShowMarker(int index, ShootableTarget target, Color color, string text, VisualElement root)
        {
            if (target == null || target.IsDestroyed || view == null) return;
            Vector3 point = view.WorldToScreenPoint(target.transform.position);
            if (point.z <= 0f || point.x < 0f || point.x > Screen.width || point.y < 0f || point.y > Screen.height) return;
            VisualElement marker = markers[index];
            marker.style.display = DisplayStyle.Flex;
            marker.style.left = point.x / Screen.width * root.resolvedStyle.width - 24;
            marker.style.top = (1f - point.y / Screen.height) * root.resolvedStyle.height - 24;
            foreach (VisualElement corner in marker.Children())
            {
                corner.style.borderLeftColor = corner.style.borderRightColor = color;
                corner.style.borderTopColor = corner.style.borderBottomColor = color;
            }
            numbers[index].style.color = color;
            numbers[index].text = text;
        }

        private void Cockpit(MissileLockState state)
        {
            ControllerService service = ControllerService.Current;
            if (service == null) return;
            if (writeCockpit) service.Display.Show(LcdGlyph.Spread("MISSILES", state.Ammo + "/" + state.Capacity),
                !weapon.Available ? "RESTARTING" : Time.timeScale <= 0f ? "PAUSED"
                : state.IsPainting ? "LOCK " + state.Locks.Count + " RELEASE"
                : service.State.HasLock ? "HOLD LOCK BUTTON" : "HOLD SHIFT: LOCK");
            int step = state.IsPainting ? state.Locks.Count * 4 + Mathf.FloorToInt(state.Progress * 3f) : -1;
            if (weapon.MissilesFired > lastFired) service.PlayTone(660, 120);
            else if (state.Locks.Count > lastLocks || step > toneStep)
                service.PlayTone(440 + Mathf.Max(0, step) * 35, 55);
            toneStep = step;
            lastLocks = state.Locks.Count;
            lastFired = weapon.MissilesFired;
        }

        private void OnDisable() => overlay?.RemoveFromHierarchy();
    }
}

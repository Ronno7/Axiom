using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>The weapons instrument, laid out like a stores page, and the target markers. MASTER ARM leads as a bar:
    /// hatched SAFE or solid ARMED. Below it each weapon is marked x while it can't fire and framed in angle brackets
    /// while it can. SAFE also shows where the eye already is: the aim diamonds dim under a quiet X, which turns bright when
    /// a lesson calls for arming or a press is refused, and only an armed gun brackets its target. Gun, acquiring and locked
    /// brackets are one family of corners. Reads the weapons; never changes them.</summary>
    [DefaultExecutionOrder(110)] // After the chase camera and MenuLook's panel routing.
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class MissileDisplay : MonoBehaviour
    {
        private const float FlareSeconds = 0.5f, RefusedSeconds = 0.5f, SafeMarkSize = 58f;
        private const int GunMarker = 9, CandidateMarker = 8;

        [SerializeField] private LockOnWeapon weapon;
        [SerializeField, Min(0f)] private float bottomMargin = 24f;
        [Tooltip("The aim diamonds, which dim while SAFE.")]
        [SerializeField] private Renderer[] reticles = new Renderer[0];

        private static readonly Color Cyan = AxiomPalette.Cyan;
        private static readonly Color Ready = AxiomPalette.Light;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly VisualElement[] markers = new VisualElement[10];
        private readonly Label[] numbers = new Label[10];
        private readonly VisualElement[] pips = new VisualElement[8];
        private UIDocument document;
        private VisualElement overlay, panel, edge;
        private ArmBar armBar;
        private StoreRow gunRow, missileRow, empRow;
        private Label armLabel, count, empState, status, safeWord;
        private Cross safeMark;
        private EmpWeapon emp;
        private PlayerWeapon gun;
        private MasterArmInput authority;
        private LevelFlow flow;
        private Camera view;
        private MaterialPropertyBlock tint;
        private int lastLocks, lastFired, toneStep = -1;
        private float shownAmmo, flareUntil, refusedUntil;
        private bool armKnown, wasArmed, tinted;

        /// <summary>Something needs weapons now: while SAFE, the X, the word and the bar pulse instead of sitting quiet.</summary>
        public bool CallForArming { get; set; }

        /// <summary>0–1 while an entry draws the instrument in: its edge draws up, the bar types in, the stores appear in
        /// turn. Shows no target markers and no cockpit cues until 1.</summary>
        public float Assembly { get; set; } = 1f;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            view = Camera.main;
            emp = weapon != null ? weapon.GetComponent<EmpWeapon>() : null;
            gun = weapon != null ? weapon.GetComponent<PlayerWeapon>() : null;
            authority = weapon != null ? weapon.GetComponent<MasterArmInput>() : null;
            flow = FindAnyObjectByType<LevelFlow>();
            tint = new MaterialPropertyBlock();
        }

        /// <summary>Nothing to report under the pause menu, while the ship is being rebuilt or on the results.</summary>
        private bool PanelStandsDown => flow != null
            && (flow.State != RunState.Playing || !flow.EntryHeld && !weapon.Available);

        private void Build(VisualElement root)
        {
            overlay?.RemoveFromHierarchy();
            overlay = new VisualElement { name = "missile-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
            root.Add(overlay);

            panel = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.style.position = Position.Absolute;
            panel.style.right = 56;
            panel.style.bottom = bottomMargin;
            panel.style.width = 340;
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

            armBar = new ArmBar();
            armBar.style.height = 34;
            armBar.style.marginBottom = 10;
            armBar.style.alignItems = Align.Center;
            armBar.style.justifyContent = Justify.Center;
            armBar.style.display = authority != null ? DisplayStyle.Flex : DisplayStyle.None;
            panel.Add(armBar);
            armLabel = new Label { pickingMode = PickingMode.Ignore };
            armLabel.style.fontSize = 22;
            armLabel.style.letterSpacing = 6;
            armLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            armLabel.style.paddingLeft = armLabel.style.paddingRight = 8;
            armLabel.style.paddingTop = armLabel.style.paddingBottom = 0;
            armBar.Add(armLabel);

            gunRow = new StoreRow("GUN");
            panel.Add(gunRow.Root);

            missileRow = new StoreRow("MSL");
            panel.Add(missileRow.Root);
            var bank = new VisualElement { pickingMode = PickingMode.Ignore };
            bank.style.flexDirection = FlexDirection.Row;
            bank.style.alignItems = Align.Center;
            missileRow.Value.Add(bank);
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                pips[i].style.width = 26;
                pips[i].style.height = 12;
                pips[i].style.marginRight = 4;
                pips[i].style.borderLeftWidth = pips[i].style.borderRightWidth = 1;
                pips[i].style.borderTopWidth = pips[i].style.borderBottomWidth = 1;
                bank.Add(pips[i]);
            }
            count = StoreValue();
            count.style.marginLeft = 6;
            bank.Add(count);

            empRow = new StoreRow("EMP");
            panel.Add(empRow.Root);
            empState = StoreValue();
            empRow.Value.Add(empState);

            status = new Label { pickingMode = PickingMode.Ignore };
            status.style.fontSize = 14;
            status.style.letterSpacing = 2;
            status.style.color = Cyan;
            status.style.paddingLeft = status.style.paddingRight = 0;
            status.style.paddingTop = 2;
            status.style.paddingBottom = 0;
            status.style.marginTop = 4;
            status.style.minHeight = 20; // Holds its line when empty, so the panel never changes height.
            panel.Add(status);
            safeMark = new Cross(2.5f) { pickingMode = PickingMode.Ignore };
            safeMark.style.position = Position.Absolute;
            safeMark.style.width = safeMark.style.height = SafeMarkSize;
            overlay.Add(safeMark);
            safeWord = new Label("SAFE") { pickingMode = PickingMode.Ignore };
            safeWord.style.position = Position.Absolute;
            safeWord.style.fontSize = 20;
            safeWord.style.letterSpacing = 5;
            safeWord.style.unityFontStyleAndWeight = FontStyle.Bold;
            safeWord.style.paddingLeft = safeWord.style.paddingRight = 6;
            safeWord.style.paddingTop = safeWord.style.paddingBottom = 0;
            safeWord.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.75f);
            overlay.Add(safeWord);

            for (int i = 0; i < markers.Length; i++)
            {
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.style.position = Position.Absolute;
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
                numbers[i].style.width = 120;
                numbers[i].style.fontSize = 13;
                numbers[i].style.unityFontStyleAndWeight = FontStyle.Bold;
                marker.Add(numbers[i]);
                markers[i] = marker;
                overlay.Add(marker);
            }
        }

        private static Label StoreValue()
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 18;
            label.style.letterSpacing = 1;
            label.style.paddingLeft = label.style.paddingRight = 0;
            label.style.paddingTop = label.style.paddingBottom = 0;
            return label;
        }

        private void LateUpdate()
        {
            if (weapon == null || weapon.State == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
            if (overlay == null || overlay.parent != root) Build(root);
            panel.style.display = PanelStandsDown ? DisplayStyle.None : DisplayStyle.Flex;
            MissileLockState state = weapon.State;
            bool armed = authority == null || authority.Armed;
            WatchMasterArm(armed);
            ShowArmBar(armed);
            ShowStores(state, armed);
            ShowSafeMark(armed, root);
            TintReticles(armed);
            ApplyAssembly(state);
            for (int i = 0; i < markers.Length; i++) markers[i].style.display = DisplayStyle.None;
            if (Assembly < 1f) return;
            if (weapon.Available && Time.timeScale > 0f)
            {
                if (armed && gun != null) ShowMarker(GunMarker, gun.AssistTarget, Cyan, string.Empty, root, 44f);
                for (int i = 0; i < state.Locks.Count; i++)
                    ShowMarker(i, weapon.Target(state.Locks[i]), Ready, "LOCK " + (i + 1), root, 48f);
                // Acquiring closes in on the target as it completes.
                ShowMarker(CandidateMarker, weapon.Target(state.CandidateId), Color.Lerp(Cyan * 0.55f, Ready, state.Progress),
                    "ACQUIRE " + Mathf.RoundToInt(state.Progress * 100f) + "%", root, Mathf.Lerp(80f, 48f, state.Progress));
            }
            Cockpit(state);
        }

        /// <summary>The moment of arming: the diamonds flare and the cockpit answers. A press while SAFE is refused aloud.</summary>
        private void WatchMasterArm(bool armed)
        {
            if (authority == null) return;
            if (armKnown && armed != wasArmed && Assembly >= 1f)
            {
                if (armed) flareUntil = Time.unscaledTime + FlareSeconds;
                ControllerService.Current?.PlayTone(armed ? 880 : 330, armed ? 160 : 90);
            }
            armKnown = true;
            wasArmed = armed;
            if (authority.RefusedThisFrame)
            {
                refusedUntil = Time.unscaledTime + RefusedSeconds;
                ControllerService.Current?.PlayTone(180, 140);
            }
        }

        private void ShowArmBar(bool armed)
        {
            if (authority == null) return;
            Color safeColour = SafeColour(AxiomPalette.Text);
            armBar.Set(armed, armed ? Cyan : safeColour);
            armLabel.text = armed ? "ARMED" : "SAFE";
            armLabel.style.color = armed ? AxiomPalette.Void : safeColour;
            armLabel.style.backgroundColor = armed ? AxiomPalette.Clear : AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f);
        }

        private Color SafeColour(Color quiet) => Time.unscaledTime < refusedUntil ? AxiomPalette.Ember
            : CallForArming ? Color.Lerp(AxiomPalette.Text, Ready, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f)) : quiet;

        private void ShowStores(MissileLockState state, bool armed)
        {
            bool safe = !armed;
            gunRow.Show(safe, armed);
            missileRow.Show(safe, armed && state.Ammo > 0);
            shownAmmo = state.Ammo < shownAmmo ? state.Ammo : Mathf.MoveTowards(shownAmmo, state.Ammo, Time.unscaledDeltaTime * 6f);
            count.text = state.Ammo + "/" + state.Capacity;
            count.style.color = state.Ammo > 0 && armed ? Ready : AxiomPalette.Muted;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].style.display = i < state.Capacity ? DisplayStyle.Flex : DisplayStyle.None;
                Color frame = armed ? Cyan : AxiomPalette.Muted;
                pips[i].style.borderLeftColor = pips[i].style.borderRightColor = frame;
                pips[i].style.borderTopColor = pips[i].style.borderBottomColor = frame;
                pips[i].style.backgroundColor = i < state.Locks.Count ? Ready
                    : i < Mathf.FloorToInt(shownAmmo) ? armed ? Cyan : AxiomPalette.Muted
                    : AxiomPalette.WithAlpha(Cyan, i == state.Ammo && !state.Unloaded ? state.RefillProgress * 0.4f : 0f);
            }
            if (emp != null)
            {
                bool charged = emp.State.Ready;
                empRow.Root.style.display = DisplayStyle.Flex;
                empRow.Show(safe, armed && charged);
                empState.text = emp.Pulsing ? "DISCHARGED" : emp.State.Offline ? "OFFLINE" : charged ? "READY" : "SPENT";
                empState.style.color = charged && armed ? Ready : AxiomPalette.Muted;
            }
            else empRow.Root.style.display = DisplayStyle.None;
            status.text = !weapon.Available ? string.Empty : Status(state);
        }

        private string Status(MissileLockState state)
        {
            if (state.Unloaded) return state.IsPainting ? "NO MISSILES ISSUED" : string.Empty;
            if (state.IsPainting)
                return state.Locks.Count > 0 ? state.Locks.Count + " LOCKED  /  RELEASE TO FIRE"
                    : state.Ammo == 0 ? "RELOADING" : weapon.CloseTargetInCone ? "TOO CLOSE TO LOCK" : "PAINTING";
            if (weapon.CloseTargetInCone) return "TOO CLOSE TO LOCK";
            return $"LOCK RANGE {weapon.MinimumRange:0}–{weapon.MaximumRange:0} m";
        }

        /// <summary>While SAFE, an X and the word sit on the aim point: normal flight stays quiet, and the exception
        /// shows where the eye already is. A refused press turns them Ember.</summary>
        private void ShowSafeMark(bool armed, VisualElement root)
        {
            bool show = !armed && Assembly >= 1f && authority != null && authority.WeaponsAvailable && weapon.Available
                && Time.timeScale > 0f && view != null && gun != null;
            Vector3 point = show ? view.WorldToScreenPoint(gun.AimPoint.position) : Vector3.zero;
            show &= point.z > 0f;
            safeMark.style.display = safeWord.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            float x = point.x / Screen.width * root.resolvedStyle.width;
            float y = (1f - point.y / Screen.height) * root.resolvedStyle.height;
            Color colour = SafeColour(AxiomPalette.WithAlpha(AxiomPalette.Muted, 0.6f));
            safeMark.style.left = x - SafeMarkSize * 0.5f;
            safeMark.style.top = y - SafeMarkSize * 0.5f;
            safeMark.Colour = colour;
            safeWord.style.left = x + SafeMarkSize * 0.5f + 8f;
            safeWord.style.top = y - 16f;
            safeWord.style.color = colour;
        }

        private void TintReticles(bool armed)
        {
            float flare = Mathf.Clamp01((flareUntil - Time.unscaledTime) / FlareSeconds);
            bool tinting = authority != null && (!armed || flare > 0f);
            if (!tinting && !tinted) return;
            tinted = tinting;
            foreach (Renderer reticle in reticles)
            {
                if (reticle == null) continue;
                if (!tinting)
                {
                    reticle.SetPropertyBlock(null);
                    continue;
                }
                Material material = reticle.sharedMaterial;
                Color own = material.GetColor(BaseColor);
                tint.Clear();
                tint.SetColor(BaseColor, !armed ? own * 0.3f
                    : Color.Lerp(own, AxiomPalette.MaterialColor(material, "_BaseColor", AxiomPalette.Light, 3f), flare));
                reticle.SetPropertyBlock(tint);
            }
        }

        private void ApplyAssembly(MissileLockState state)
        {
            float a = Mathf.Clamp01(Assembly);
            bool done = a >= 1f;
            edge.style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
            panel.style.borderLeftWidth = done ? 2 : 0;
            panel.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f * Mathf.InverseLerp(0.1f, 0.4f, a));
            if (!done) edge.style.scale = new Scale(new Vector3(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.25f, a)), 1f));
            StyleEnum<Visibility> Stage(float from) => done ? new StyleEnum<Visibility>(StyleKeyword.Null)
                : a >= from ? Visibility.Visible : Visibility.Hidden;
            armBar.style.visibility = Stage(0.12f);
            gunRow.Root.style.visibility = Stage(0.35f);
            missileRow.Root.style.visibility = Stage(0.5f);
            empRow.Root.style.visibility = Stage(0.8f);
            status.style.visibility = Stage(1f);
            if (done)
            {
                foreach (VisualElement pip in pips) pip.style.visibility = StyleKeyword.Null;
                return;
            }
            armLabel.text = InstrumentBuild.Reveal(armLabel.text, Mathf.InverseLerp(0.12f, 0.35f, a));
            count.text = InstrumentBuild.Reveal(count.text, Mathf.InverseLerp(0.7f, 0.85f, a));
            empState.text = InstrumentBuild.Reveal(empState.text, Mathf.InverseLerp(0.82f, 0.98f, a));
            for (int i = 0; i < pips.Length && i < state.Capacity; i++)
                pips[i].style.visibility = a >= 0.5f + i * 0.05f ? Visibility.Visible : Visibility.Hidden;
        }

        private void ShowMarker(int index, ShootableTarget target, Color color, string text, VisualElement root, float size)
        {
            if (target == null || target.IsDestroyed || view == null) return;
            Vector3 point = view.WorldToScreenPoint(target.transform.position);
            if (point.z <= 0f || point.x < 0f || point.x > Screen.width || point.y < 0f || point.y > Screen.height) return;
            VisualElement marker = markers[index];
            marker.style.display = DisplayStyle.Flex;
            marker.style.width = marker.style.height = size;
            marker.style.left = point.x / Screen.width * root.resolvedStyle.width - size * 0.5f;
            marker.style.top = (1f - point.y / Screen.height) * root.resolvedStyle.height - size * 0.5f;
            foreach (VisualElement corner in marker.Children())
            {
                corner.style.borderLeftColor = corner.style.borderRightColor = color;
                corner.style.borderTopColor = corner.style.borderBottomColor = color;
            }
            // Beside the corners, never below them, where the aim diamonds usually sit.
            numbers[index].style.left = size + 6f;
            numbers[index].style.top = size * 0.5f - 9f;
            numbers[index].style.color = color;
            numbers[index].text = text;
        }

        private void Cockpit(MissileLockState state)
        {
            ControllerService service = ControllerService.Current;
            if (service == null) return;
            int step = state.IsPainting ? state.Locks.Count * 4 + Mathf.FloorToInt(state.Progress * 3f) : -1;
            if (weapon.MissilesFired > lastFired) service.PlayTone(660, 120);
            else if (state.Locks.Count > lastLocks || step > toneStep)
                service.PlayTone(440 + Mathf.Max(0, step) * 35, 55);
            toneStep = step;
            lastLocks = state.Locks.Count;
            lastFired = weapon.MissilesFired;
        }

        private void OnDisable()
        {
            overlay?.RemoveFromHierarchy();
            foreach (Renderer reticle in reticles) if (reticle != null) reticle.SetPropertyBlock(null);
            tinted = false;
        }

        /// <summary>One weapon's row: marked x while it can't fire, framed in angle brackets while it can.</summary>
        private sealed class StoreRow
        {
            public readonly VisualElement Root, Value;
            private readonly Label open, name, close;

            public StoreRow(string weapon)
            {
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.style.flexDirection = FlexDirection.Row;
                Root.style.alignItems = Align.Center;
                Root.style.height = 28;
                open = Bracket("<");
                Root.Add(open);
                var nameBox = new VisualElement { pickingMode = PickingMode.Ignore };
                nameBox.style.width = 52;
                Root.Add(nameBox);
                name = new Label(weapon) { pickingMode = PickingMode.Ignore };
                name.style.fontSize = 22;
                name.style.letterSpacing = 2;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.style.paddingLeft = name.style.paddingRight = 0;
                name.style.paddingTop = name.style.paddingBottom = 0;
                nameBox.Add(name);
                close = Bracket(">");
                Root.Add(close);
                Value = new VisualElement { pickingMode = PickingMode.Ignore };
                Value.style.flexGrow = 1;
                Value.style.flexDirection = FlexDirection.Row;
                Value.style.justifyContent = Justify.FlexEnd;
                Value.style.alignItems = Align.Center;
                Root.Add(Value);
            }

            private static Label Bracket(string glyph)
            {
                var label = new Label(glyph) { pickingMode = PickingMode.Ignore };
                label.style.width = 18;
                label.style.fontSize = 22;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.color = Ready;
                label.style.paddingLeft = label.style.paddingRight = 0;
                label.style.paddingTop = label.style.paddingBottom = 0;
                return label;
            }

            public void Show(bool crossed, bool ready)
            {
                open.text = crossed ? "x" : "<";
                open.style.color = crossed ? AxiomPalette.Text : Ready;
                open.style.visibility = ready || crossed ? Visibility.Visible : Visibility.Hidden;
                close.style.visibility = ready ? Visibility.Visible : Visibility.Hidden;
                name.style.color = ready ? Ready : AxiomPalette.Muted;
            }
        }

        /// <summary>An X corner to corner: the F/A-18's cue that a weapon can't fire.</summary>
        private sealed class Cross : VisualElement
        {
            private readonly float width;
            private Color colour = AxiomPalette.Text;

            public Cross(float width)
            {
                this.width = width;
                generateVisualContent = Draw;
            }

            public Color Colour
            {
                set
                {
                    if (value == colour) return;
                    colour = value;
                    MarkDirtyRepaint();
                }
            }

            private void Draw(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (w <= 0f || h <= 0f) return;
                Painter2D painter = context.painter2D;
                painter.lineWidth = width;
                painter.strokeColor = colour;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, 0f));
                painter.LineTo(new Vector2(w, h));
                painter.MoveTo(new Vector2(w, 0f));
                painter.LineTo(new Vector2(0f, h));
                painter.Stroke();
            }
        }

        /// <summary>MASTER ARM's bar: diagonal hatching while SAFE, solid while ARMED, so the shape carries it as well as
        /// the colour.</summary>
        private sealed class ArmBar : VisualElement
        {
            private const float Spacing = 18f;
            private bool solid;
            private Color colour;

            public ArmBar()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent = Draw;
            }

            public void Set(bool isSolid, Color newColour)
            {
                if (isSolid == solid && newColour == colour) return;
                solid = isSolid;
                colour = newColour;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (w <= 0f || h <= 0f) return;
                Painter2D painter = context.painter2D;
                painter.strokeColor = colour;
                painter.fillColor = colour;
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(1f, 1f));
                painter.LineTo(new Vector2(w - 1f, 1f));
                painter.LineTo(new Vector2(w - 1f, h - 1f));
                painter.LineTo(new Vector2(1f, h - 1f));
                painter.ClosePath();
                if (solid) painter.Fill();
                else painter.Stroke();
                if (solid) return;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                for (float x = 1f; x + h < w; x += Spacing)
                {
                    painter.MoveTo(new Vector2(x, h - 1f));
                    painter.LineTo(new Vector2(x + h - 2f, 1f));
                }
                painter.Stroke();
            }
        }
    }
}

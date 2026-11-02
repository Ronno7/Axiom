using GeometricFlight.Combat;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>The weapons panel (WeaponsPanel.uxml), laid out like a stores page. MASTER ARM leads as a bar: hatched SAFE or
    /// solid ARMED. Below it each weapon is marked x while it can't fire and framed in angle brackets while it can.</summary>
    public sealed class WeaponsPanel
    {
        private static readonly Color Cyan = AxiomPalette.Cyan;
        private static readonly Color Ready = AxiomPalette.Light;

        private readonly LockOnWeapon weapon;
        private readonly EmpWeapon emp;
        private readonly VisualElement panel, edge;
        private readonly ArmBar armBar;
        private readonly Label armLabel, count, empState, status;
        private readonly StoreRow gunRow, missileRow, empRow;
        private readonly VisualElement[] pips;
        private float shownAmmo;

        public WeaponsPanel(VisualElement root, LockOnWeapon weapon)
        {
            this.weapon = weapon;
            emp = weapon.GetComponent<EmpWeapon>();
            panel = root.Q("weapons-panel");
            edge = panel.Q("weapons-panel-edge");
            armBar = panel.Q<ArmBar>("arm-bar");
            armLabel = panel.Q<Label>("arm-label");
            gunRow = new StoreRow(panel.Q("store-gun"));
            missileRow = new StoreRow(panel.Q("store-msl"));
            empRow = new StoreRow(panel.Q("store-emp"));
            count = panel.Q<Label>("store-count");
            empState = panel.Q<Label>("store-emp-state");
            status = panel.Q<Label>("weapons-status");
            pips = panel.Query(className: "store-pip").ToList().ToArray();
        }

        public bool IsAttached => panel.panel != null;

        public void Hide() => panel.style.display = DisplayStyle.None;

        /// <param name="safeColour">The SAFE bar's colour, which pulses when a lesson calls for arming.</param>
        /// <param name="assembly">0–1 while an entry draws the panel in.</param>
        public void Show(MissileLockState state, bool armed, Color safeColour, float assembly, bool standsDown)
        {
            panel.style.display = standsDown ? DisplayStyle.None : DisplayStyle.Flex;
            ShowArmBar(armed, safeColour);
            ShowStores(state, armed);
            ApplyAssembly(state, assembly);
        }

        private void ShowArmBar(bool armed, Color safeColour)
        {
            armBar.Set(armed, armed ? Cyan : safeColour);
            armLabel.text = armed ? "ARMED" : "SAFE";
            armLabel.style.color = armed ? AxiomPalette.Void : safeColour;
            armLabel.style.backgroundColor = armed ? AxiomPalette.Clear : AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f);
        }

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
            bool charged = emp.State.Ready;
            empRow.Show(safe, armed && charged);
            empState.text = emp.Pulsing ? "DISCHARGED" : emp.State.Offline ? "OFFLINE" : charged ? "READY" : "SPENT";
            empState.style.color = charged && armed ? Ready : AxiomPalette.Muted;
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

        /// <summary>The edge draws up, the bar types in, the stores appear in turn. Inline styles only while partial, so
        /// the USS look holds once it's done.</summary>
        private void ApplyAssembly(MissileLockState state, float assembly)
        {
            float a = Mathf.Clamp01(assembly);
            bool done = a >= 1f;
            edge.style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
            panel.style.borderLeftWidth = done ? new StyleFloat(StyleKeyword.Null) : 0f;
            panel.style.backgroundColor = done ? new StyleColor(StyleKeyword.Null)
                : AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f * Mathf.InverseLerp(0.1f, 0.4f, a));
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

        /// <summary>One weapon's row: marked x while it can't fire, framed in angle brackets while it can.</summary>
        private sealed class StoreRow
        {
            public readonly VisualElement Root;
            private readonly Label open, name, close;

            public StoreRow(VisualElement root)
            {
                Root = root;
                open = root.Q<Label>(className: "store-open");
                name = root.Q<Label>(className: "store-name");
                close = root.Q<Label>(className: "store-close");
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
    }
}

using GeometricFlight.Core;
using GeometricFlight.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>Objective, arrival countdown and recovery beat on the same tube as the missile HUD.</summary>
    [DefaultExecutionOrder(110), RequireComponent(typeof(UIDocument))]
    public sealed class ArenaEncounterDisplay : MonoBehaviour
    {
        [SerializeField] private ArenaEncounter encounter;
        private UIDocument document;
        private VisualElement overlay, panel, bar;
        private Label title, detail, guide;
        private Camera view;
        private ArenaWavePhase lastPhase;
        private static readonly Color Cyan = AxiomPalette.Cyan;
        private static readonly Color Orange = AxiomPalette.Orange;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            view = Camera.main;
        }

        private void Build(VisualElement root)
        {
            overlay?.RemoveFromHierarchy();
            overlay = new VisualElement { name = "encounter-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
            root.Add(overlay);
            panel = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.style.position = Position.Absolute;
            panel.style.top = 26;
            panel.style.left = Length.Percent(50);
            panel.style.marginLeft = -210;
            panel.style.width = 420;
            panel.style.paddingTop = panel.style.paddingBottom = 10;
            panel.style.backgroundColor = AxiomPalette.WithAlpha(AxiomPalette.Void, 0.85f);
            overlay.Add(panel);
            title = new Label { pickingMode = PickingMode.Ignore };
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(title);
            detail = new Label { pickingMode = PickingMode.Ignore };
            detail.style.fontSize = 13;
            detail.style.color = AxiomPalette.Light;
            detail.style.unityTextAlign = TextAnchor.MiddleCenter;
            detail.style.marginTop = 4;
            panel.Add(detail);
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 3;
            bar.style.marginTop = 8;
            panel.Add(bar);
            guide = new Label { pickingMode = PickingMode.Ignore };
            guide.style.position = Position.Absolute;
            guide.style.width = 170;
            guide.style.unityTextAlign = TextAnchor.MiddleCenter;
            guide.style.color = Orange;
            guide.style.fontSize = 13;
            overlay.Add(guide);
        }

        private void LateUpdate()
        {
            if (encounter == null || encounter.State == null) return;
            VisualElement root = document.rootVisualElement;
            if (root == null) return;
            if (overlay == null || overlay.parent != root) Build(root);
            ArenaWaveState wave = encounter.State;
            overlay.style.display = encounter.isActiveAndEnabled && wave.Phase != ArenaWavePhase.Inactive
                ? DisplayStyle.Flex : DisplayStyle.None;
            bool incoming = wave.Phase == ArenaWavePhase.Incoming;
            Color accent = incoming ? Orange : Cyan;
            title.style.color = accent;
            bar.style.backgroundColor = accent;
            bar.style.width = Length.Percent(incoming ? wave.WarningProgress * 100f : 100f);
            if (Time.timeScale <= 0f)
            {
                title.text = "PAUSED";
                detail.text = "ROUND " + wave.Round.ToString("00");
            }
            else if (incoming)
            {
                title.text = "PROGRAMS INCOMING  " + wave.TimeLeft.ToString("0.0") + "s";
                detail.text = "ROUND " + wave.Round.ToString("00") + "  /  CLEAR ALL " + wave.Total + " TARGETS";
            }
            else if (wave.Phase == ArenaWavePhase.Combat)
            {
                title.text = "CLEAR THE SECTOR";
                detail.text = "ROUND " + wave.Round.ToString("00") + "  /  " + wave.Remaining + " TARGETS REMAIN";
            }
            else if (wave.Phase == ArenaWavePhase.Cleared)
            {
                title.text = "SECTOR CLEAR";
                detail.text = "SHIELD + MISSILES RESTORED  /  NEXT ROUND " + Mathf.CeilToInt(wave.TimeLeft) + "s";
            }
            else
            {
                title.text = "RECONSTRUCTING";
                detail.text = "RETRY ROUND " + wave.Round.ToString("00");
            }
            GuideToPortals(incoming && Time.timeScale > 0f, root);
            if (wave.Phase != lastPhase)
            {
                if (incoming) ControllerService.Current?.PlayTone(440, 180);
                else if (wave.Phase == ArenaWavePhase.Cleared) ControllerService.Current?.PlayTone(1320, 220);
                lastPhase = wave.Phase;
            }
        }

        private void GuideToPortals(bool show, VisualElement root)
        {
            guide.style.display = show && view != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show || view == null) return;
            Vector3 nearest = encounter.SpawnPosition(0);
            for (int i = 1; i < encounter.SpawnCount; i++)
                if ((encounter.SpawnPosition(i) - view.transform.position).sqrMagnitude < (nearest - view.transform.position).sqrMagnitude)
                    nearest = encounter.SpawnPosition(i);
            Vector3 point = view.WorldToViewportPoint(nearest);
            Vector2 direction = new Vector2(point.x - 0.5f, 0.5f - point.y);
            if (point.z < 0f) direction = -direction;
            float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
            float x = w * (0.5f + direction.x), y = h * (0.5f + direction.y);
            bool offscreen = point.z < 0f || point.x < 0.05f || point.x > 0.95f || point.y < 0.1f || point.y > 0.9f;
            if (offscreen)
            {
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
                Vector2 edge = new Vector2(direction.x * w, direction.y * h);
                edge *= Mathf.Min((w * 0.5f - 100f) / Mathf.Max(0.001f, Mathf.Abs(edge.x)),
                    (h * 0.5f - 110f) / Mathf.Max(0.001f, Mathf.Abs(edge.y)));
                x = w * 0.5f + edge.x;
                y = h * 0.5f + edge.y;
            }
            guide.style.left = Mathf.Clamp(x - 85f, 16f, w - 186f);
            guide.style.top = Mathf.Clamp(y + 24f, 120f, h - 125f);
            guide.text = (offscreen ? "TURN TO PORTALS" : "PORTAL") + "  "
                + Mathf.RoundToInt(Vector3.Distance(view.transform.position, nearest)) + " m";
        }

        private void OnDisable() => overlay?.RemoveFromHierarchy();
    }
}

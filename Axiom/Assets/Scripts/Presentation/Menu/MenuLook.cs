using System;
using GeometricFlight.Core;
using GeometricFlight.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>The main menu's look in one Inspector: colour grade, atmosphere, interface details and the
    /// occasional signal glitch, plus Arcade Mode (the shared <see cref="ArcadeProfile"/>) when the player turns it
    /// on in Options. Presentation only; it never changes menu behaviour. Settings apply live in the editor; glitch events play in Play Mode
    /// (or hold the peak frame to tune it in either mode).</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Volume))]
    public sealed class MenuLook : MonoBehaviour
    {
        // ---------------- Settings ----------------

        [Serializable]
        public sealed class InterfaceSettings
        {
            [Tooltip("Dark gradient behind the menu column.")]
            public bool scrim = true;
            [Range(0f, 1f)] public float scrimOpacity = 1f;
            [Tooltip("Scrim width, percent of the screen.")]
            [Range(20f, 100f)] public float scrimWidth = 62f;
            [Tooltip("Corner brackets on the safe area.")]
            public bool frame = true;
            [Range(0f, 1f)] public float frameOpacity = 0.35f;
            [Range(0f, 120f)] public float frameInset = 40f;
            [Tooltip("System, cockpit link and build, bottom left.")]
            public bool statusLine = true;
            [Tooltip("Key hints, bottom right.")]
            public bool keyHints = true;
            [Tooltip("Red tick beside the selected menu item.")]
            public bool focusMarker = true;
        }

        [Serializable]
        public sealed class GlitchSettings
        {
            [Tooltip("A short signal glitch every so often, only while the menu is idle.")]
            public bool enabled = true;
            [Tooltip("Seconds between events, picked at random in this range.")]
            public Vector2 interval = new Vector2(8f, 12f);
            [Tooltip("Events wait until there has been no input for this many seconds.")]
            [Min(0f)] public float idleDelay = 4f;
            [Range(0f, 2f)] public float intensity = 1f;
            [Tooltip("Seconds per glitch frame. 0.04 = 25 fps; larger plays slower.")]
            [Range(0.02f, 0.12f)] public float frameTime = 0.04f;
            [Tooltip("Freeze on the event's peak frame, to tune it (editor only; ignored in builds). Title, construct and status effects show in Play Mode only.")]
            public bool holdPeak;
            [Header("Parts")]
            public bool tears = true;
            public bool blocks = true;
            [Tooltip("Offset copy in the accent red.")]
            public bool echo = true;
            public bool scanlines = true;
            [Tooltip("Radial red fringing that pulses with the event.")]
            public bool fringe = true;
            [Tooltip("A faint bar that rolls down the whole screen, UI included.")]
            public bool rollBar = true;
            [Range(0f, 0.3f)] public float rollBarStrength = 0.08f;
            public bool title = true;
            public bool statusScramble = true;
            [Tooltip("Snaps out of place for a few frames.")]
            public Transform snapConstruct;
            [Tooltip("Drops out (renderers off) around the peak.")]
            public GameObject dropConstruct;
        }

        [Header("References")]
        [SerializeField] private Camera menuCamera;
        [SerializeField] private UIDocument menuDocument;
        [Tooltip("The 3D title's render texture (MenuTitle camera).")]
        [SerializeField] private RenderTexture titleTexture;
        [SerializeField] private Shader glowShader;
        [SerializeField] private Shader titleGlitchShader;
        [Tooltip("The game's single accent colour: focus, danger, glitch echo and fringing.")]
        [SerializeField] private Color accent = new Color(0.84f, 0.12f, 0.16f);

        [Header("Main look")]
        [SerializeField] private GradeSettings grade = new GradeSettings();
        [SerializeField] private AtmosphereSettings atmosphere = new AtmosphereSettings();
        [SerializeField, InspectorName("Interface")] private InterfaceSettings ui = new InterfaceSettings();
        [SerializeField] private GlitchSettings glitch = new GlitchSettings();

        [Header("Arcade mode (player toggle in Options)")]
        [Tooltip("Shared with the levels' LevelLook, so the look is tuned in one place.")]
        [SerializeField] private ArcadeProfile arcade;
        [Tooltip("Show Arcade Mode in the editor whatever the player's Options setting is (ignored in builds).")]
        [SerializeField] private bool previewArcade;

        // ---------------- State ----------------

        private LookVolume look;
        private readonly ArcadeInterface screenInterface = new ArcadeInterface();
        private UIDocument[] interfaceDocuments;
        private readonly LookAtmosphere air = new LookAtmosphere();
        private bool atmosphereDirty = true;

        private readonly System.Random random = new System.Random();

        private VisualElement root, scrim, frame, rollBar, statusLine, statusLed, keyHints, title;
        private Label statusSystem, statusLink, statusBuild;
        private Focusable lastFocus;
        private float nextStatus;

        private bool eventPlaying, forceEvent, titleGlitched;
        private float eventStart, nextEvent, lastInput;
        private int eventSeed;
        private Vector3 snapBase;
        private Renderer[] dropRenderers;
        private Material titleMaterial;
        private RenderTexture titleGlitchTexture;

        private const string SystemText = "SYS ONLINE";

        // ---------------- Lifecycle ----------------

        private void OnEnable()
        {
            look = new LookVolume(GetComponent<Volume>(), "Menu look (generated)");
            atmosphereDirty = true;
            nextEvent = Time.unscaledTime + MenuGlitchTimeline.NextInterval(random, glitch.interval.x, glitch.interval.y);
            lastInput = Time.unscaledTime;
            Apply(CurrentPreview());
        }

        private void OnDisable()
        {
            if (eventPlaying) EndEvent();
            ScreenEffects.Clear();
            look?.Dispose();
            look = null;
            air.Dispose();
            screenInterface.Release();
            Generated.Release(titleMaterial);
            if (titleGlitchTexture != null) titleGlitchTexture.Release();
            Generated.Release(titleGlitchTexture);
            titleMaterial = null;
            titleGlitchTexture = null;
        }

        private void OnValidate()
        {
            atmosphereDirty = true;
            if (look != null) Apply(CurrentPreview());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (atmosphereDirty)
            {
                atmosphereDirty = false;
                air.Build(atmosphere, menuCamera, glowShader, "Menu");
            }
            if (!Application.isPlaying)
            {
                Apply(CurrentPreview());
                return;
            }

            float now = Time.unscaledTime;
            TrackInput(now);
            air.Follow(menuCamera, Time.deltaTime);
            UpdateStatus(now);

            GlitchFrame frameNow = GlitchFrame.Calm;
            if (HoldPeak)
            {
                if (!eventPlaying) StartEvent(now);
                frameNow = MenuGlitchTimeline.Frame(MenuGlitchTimeline.PeakFrame);
            }
            else if (eventPlaying)
            {
                int index = Mathf.FloorToInt((now - eventStart) / glitch.frameTime);
                if (index >= MenuGlitchTimeline.FrameCount) EndEvent();
                else frameNow = MenuGlitchTimeline.Frame(index);
            }
            else if (forceEvent || (glitch.enabled && now >= nextEvent && now - lastInput >= glitch.idleDelay))
            {
                StartEvent(now);
            }

            Apply(frameNow);
            if (eventPlaying) ApplyEventToWorld(frameNow);
        }

        /// <summary>Plays one glitch event now (Play Mode), ignoring the interval and idle delay.</summary>
        [ContextMenu("Play glitch now")]
        public void PlayGlitchNow() => forceEvent = true;

        // Editor-only tuning aids: a saved Preview Arcade or Hold Peak never reaches a build.
        private bool ArcadeOn => arcade != null && ((Application.isEditor && previewArcade) || GameSettings.ArcadeMode);
        private bool HoldPeak => Application.isEditor && glitch.holdPeak;

        private GlitchFrame CurrentPreview() =>
            HoldPeak ? MenuGlitchTimeline.Frame(MenuGlitchTimeline.PeakFrame) : GlitchFrame.Calm;

        // ---------------- Apply ----------------

        private void Apply(in GlitchFrame f)
        {
            ArcadeProfile a = ArcadeOn ? arcade : null;
            if (look != null) look.Apply(grade, a, menuCamera);
            LookAtmosphere.ApplyHaze(atmosphere, menuCamera);
            interfaceDocuments ??= new[] { menuDocument };
            screenInterface.Apply(a, menuCamera, interfaceDocuments);
            ApplyScreen(f);
            ApplyInterface(f);
        }

        private void ApplyScreen(in GlitchFrame f)
        {
            float k = glitch.intensity;
            var glitchVector = new Vector4(glitch.tears ? f.Tear * k : 0f, eventSeed + f.Seed,
                glitch.blocks ? f.Blocks * k : 0f, glitch.scanlines ? f.Scanlines * Mathf.Min(k, 1f) : 0f);
            float eventFringe = glitch.fringe ? f.Fringe * 2f * k : 0f;
            bool arcadeOn = ArcadeOn;
            float baseFringe = arcadeOn ? arcade.FringeStrength : 0f;
            var echo = new Vector4(glitch.echo ? f.EchoPixels * k : 0f, glitch.echo ? f.EchoAmount : 0f,
                Mathf.Max(arcadeOn ? arcade.FringeOffset : 0f, eventFringe > 0f ? 6f : 0f),
                Mathf.Clamp01(baseFringe + eventFringe));
            ScreenEffects.Set(menuCamera, glitchVector, echo, arcadeOn ? arcade.CrtVector : Vector4.zero,
                arcadeOn ? arcade.TubeEdge : 0f, arcadeOn ? arcade.ExtrasVector : Vector4.zero, accent);
        }

        // ---------------- Interface ----------------

        private bool BindInterface()
        {
            VisualElement current = menuDocument != null ? menuDocument.rootVisualElement : null;
            if (current == null) return false;
            if (current == root && scrim != null) return true;
            root = current;
            scrim = root.Q("scrim");
            frame = root.Q("frame");
            rollBar = root.Q("roll-bar");
            statusLine = root.Q("status-line");
            statusLed = root.Q("status-led");
            keyHints = root.Q("key-hints");
            title = root.Q("game-title");
            statusSystem = root.Q<Label>("status-system");
            statusLink = root.Q<Label>("status-link");
            statusBuild = root.Q<Label>("status-build");
            return scrim != null;
        }

        private void ApplyInterface(in GlitchFrame f)
        {
            if (!BindInterface()) return;
            scrim.style.display = ui.scrim ? DisplayStyle.Flex : DisplayStyle.None;
            scrim.style.opacity = ui.scrimOpacity;
            scrim.style.width = Length.Percent(ui.scrimWidth);
            if (frame != null)
            {
                frame.style.display = ui.frame ? DisplayStyle.Flex : DisplayStyle.None;
                frame.style.opacity = ui.frameOpacity;
                foreach (VisualElement corner in frame.Children())
                {
                    bool right = corner.ClassListContains("frame__corner--right");
                    bool bottom = corner.ClassListContains("frame__corner--bottom");
                    if (right) corner.style.right = ui.frameInset; else corner.style.left = ui.frameInset;
                    if (bottom) corner.style.bottom = ui.frameInset; else corner.style.top = ui.frameInset;
                }
            }
            if (statusLine != null) statusLine.style.display = ui.statusLine ? DisplayStyle.Flex : DisplayStyle.None;
            if (keyHints != null) keyHints.style.display = ui.keyHints ? DisplayStyle.Flex : DisplayStyle.None;
            root.EnableInClassList("no-focus-marker", !ui.focusMarker);

            if (rollBar != null)
            {
                bool show = glitch.rollBar && f.RollY >= 0f;
                rollBar.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                rollBar.style.top = Length.Percent(f.RollY * 100f);
                rollBar.style.opacity = glitch.rollBarStrength * Mathf.Min(glitch.intensity, 1f);
            }
            if (statusSystem != null)
                statusSystem.text = glitch.statusScramble && f.Scramble
                    ? MenuGlitchTimeline.Scramble(SystemText, eventSeed + f.Seed) : SystemText;
        }

        private void UpdateStatus(float now)
        {
            if (now < nextStatus || !BindInterface()) return;
            nextStatus = now + 0.5f;
            ControllerService service = ControllerService.Current;
            LinkState state = service != null ? service.Link.State : LinkState.Stopped;
            if (statusLink != null)
                statusLink.text = state == LinkState.Connected ? $"COCKPIT LINKED  {service.Link.PortName}"
                    : state == LinkState.Searching ? "COCKPIT SEARCHING" : "COCKPIT OFFLINE";
            // Red is reserved for problems: the LED turns red while the cockpit isn't linked.
            statusLed?.EnableInClassList("status-led--offline", state != LinkState.Connected);
            if (statusBuild != null) statusBuild.text = $"BUILD {Application.version}";
        }

        private void TrackInput(float now)
        {
            bool input = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                                              || Mouse.current.delta.ReadValue().sqrMagnitude > 1f));
            Focusable focused = root?.focusController?.focusedElement;
            if (focused != lastFocus)
            {
                lastFocus = focused;
                input = true;
            }
            if (input) lastInput = now;
        }

        // ---------------- Glitch event ----------------

        private void StartEvent(float now)
        {
            forceEvent = false;
            eventPlaying = true;
            eventStart = now;
            eventSeed = random.Next(1, 1000);
            if (glitch.snapConstruct != null) snapBase = glitch.snapConstruct.localPosition;
            dropRenderers = glitch.dropConstruct != null ? glitch.dropConstruct.GetComponentsInChildren<Renderer>() : null;
        }

        private void EndEvent()
        {
            eventPlaying = false;
            if (glitch.snapConstruct != null) glitch.snapConstruct.localPosition = snapBase;
            if (dropRenderers != null)
                foreach (Renderer r in dropRenderers)
                    if (r != null) r.enabled = true;
            dropRenderers = null;
            RestoreTitle();
            nextEvent = Time.unscaledTime + MenuGlitchTimeline.NextInterval(random, glitch.interval.x, glitch.interval.y);
        }

        private void ApplyEventToWorld(in GlitchFrame f)
        {
            float k = glitch.intensity;
            if (glitch.snapConstruct != null)
                glitch.snapConstruct.localPosition = snapBase + new Vector3(f.SnapX, f.SnapY, 0f) * k;
            if (dropRenderers != null)
                foreach (Renderer r in dropRenderers)
                    if (r != null) r.enabled = !f.Drop;

            if (glitch.title && (f.TitleTear > 0f || f.TitleEchoPixels > 0f) && GlitchTitle(f, k)) return;
            RestoreTitle();
        }

        private bool GlitchTitle(in GlitchFrame f, float k)
        {
            if (titleTexture == null || title == null || titleGlitchShader == null) return false;
            if (titleMaterial == null) titleMaterial = new Material(titleGlitchShader) { hideFlags = HideFlags.HideAndDontSave };
            if (titleGlitchTexture == null)
                titleGlitchTexture = new RenderTexture(titleTexture.width, titleTexture.height, 0, RenderTextureFormat.ARGB32)
                    { name = "Menu title glitch", hideFlags = HideFlags.HideAndDontSave };
            titleMaterial.SetFloat("_Tear", f.TitleTear * k);
            titleMaterial.SetFloat("_Seed", eventSeed + f.Seed + 20);
            titleMaterial.SetFloat("_Echo", glitch.echo ? f.TitleEchoPixels * k : 0f);
            titleMaterial.SetFloat("_Scan", glitch.scanlines ? f.Scanlines * 0.6f : 0f);
            titleMaterial.SetColor("_Accent", accent);
            Graphics.Blit(titleTexture, titleGlitchTexture, titleMaterial);
            title.style.backgroundImage = Background.FromRenderTexture(titleGlitchTexture);
            titleGlitched = true;
            return true;
        }

        private void RestoreTitle()
        {
            if (!titleGlitched) return;
            titleGlitched = false;
            if (title != null && titleTexture != null) title.style.backgroundImage = Background.FromRenderTexture(titleTexture);
        }
    }
}

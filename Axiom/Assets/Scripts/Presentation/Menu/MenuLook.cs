using System;
using GeometricFlight.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>The main menu's look in one Inspector: colour grade, atmosphere, interface details and the
    /// occasional signal glitch (look 7), plus optional vector-monitor extras (look 6). Presentation only; it never
    /// changes menu behaviour. Settings apply live in the editor; glitch events play in Play Mode
    /// (or hold the peak frame to tune it in either mode).</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Volume))]
    public sealed class MenuLook : MonoBehaviour
    {
        // ---------------- Settings ----------------

        [Serializable]
        public sealed class GradeSettings
        {
            [Tooltip("Tonemapping, exposure, contrast, bloom, vignette and grain on the menu camera.")]
            public bool enabled = true;
            [Range(-1f, 1f)] public float exposure = 0.15f;
            [Range(-50f, 50f)] public float contrast = 12f;
            [Header("Bloom")]
            public bool bloom = true;
            [Range(0f, 2f)] public float bloomThreshold = 0.6f;
            [Range(0f, 5f)] public float bloomIntensity = 1.4f;
            [Range(0f, 1f)] public float bloomScatter = 0.7f;
            [Header("Vignette")]
            public bool vignette = true;
            [Range(0f, 1f)] public float vignetteIntensity = 0.38f;
            [Range(0.01f, 1f)] public float vignetteSmoothness = 0.45f;
            [Header("Film grain")]
            public bool grain = true;
            public FilmGrainLookup grainType = FilmGrainLookup.Thin1;
            [Range(0f, 1f)] public float grainIntensity = 0.22f;
            [Range(0f, 1f)] public float grainResponse = 0.8f;
        }

        [Serializable]
        public sealed class AtmosphereSettings
        {
            [Tooltip("Haze, horizon band, stars and dust.")]
            public bool enabled = true;
            [Tooltip("Fog colour while on: a dark haze instead of black, so distance softens objects instead of erasing them.")]
            public Color haze = new Color(0.035f, 0.036f, 0.04f);
            [Header("Horizon band")]
            public bool horizon = true;
            public Color horizonColor = new Color(0.30f, 0.31f, 0.34f);
            [Min(10f)] public float horizonDistance = 140f;
            public Vector2 horizonSize = new Vector2(420f, 34f);
            [Range(0.5f, 6f)] public float horizonFalloff = 2.2f;
            [Header("Stars")]
            public bool stars = true;
            [Range(0, 3000)] public int starCount = 700;
            public int starSeed = 7;
            [Tooltip("Half-width of the star field, in metres.")]
            [Min(0f)] public float starSpread = 170f;
            [Tooltip("World height range, in metres.")]
            public Vector2 starHeight = new Vector2(6f, 120f);
            [Tooltip("Distance range in front of the camera, in metres.")]
            public Vector2 starDepth = new Vector2(70f, 145f);
            public Vector2 starSize = new Vector2(0.18f, 0.45f);
            [Range(0f, 2f)] public float starBrightness = 1f;
            [Header("Dust")]
            public bool dust = true;
            [Range(0, 1000)] public int dustCount = 160;
            [Min(0f)] public float dustSpread = 14f;
            public Vector2 dustHeight = new Vector2(0.4f, 9f);
            public Vector2 dustDepth = new Vector2(3f, 30f);
            public Vector2 dustSize = new Vector2(0.02f, 0.06f);
            [Range(0f, 2f)] public float dustBrightness = 0.55f;
            [Tooltip("Metres per second toward the camera; 4 matches the floor markers.")]
            [Min(0f)] public float dustDrift = 4f;
        }

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
            [Tooltip("Freeze on the event's peak frame, to tune it. Title, construct and status effects show in Play Mode only.")]
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

        [Serializable]
        public sealed class PhosphorGlowSettings
        {
            [Tooltip("Vector-monitor bloom. Replaces the grade's bloom while on.")]
            public bool enabled;
            [Range(0f, 2f)] public float threshold = 0.45f;
            [Range(0f, 5f)] public float intensity = 2f;
            [Range(0f, 1f)] public float scatter = 0.65f;
        }

        [Serializable]
        public sealed class FringingSettings
        {
            [Tooltip("Lens-style fringing in the accent red only, so the palette stays black, white and red.")]
            public bool enabled;
            [Tooltip("Echo offset at the screen edge, in pixels; zero at the centre.")]
            [Range(0f, 12f)] public float offset = 3f;
            [Range(0f, 1f)] public float strength = 0.6f;
        }

        [Serializable]
        public sealed class CrtSettings
        {
            [Tooltip("Tube curvature, scanlines, a monochrome aperture grille and darkened tube edges.")]
            public bool enabled;
            [Range(0f, 0.3f)] public float curvature = 0.035f;
            [Range(0f, 1f)] public float scanlines = 0.28f;
            [Tooltip("Scanline period in pixels.")]
            [Range(2f, 8f)] public float scanlinePeriod = 3f;
            [Range(0f, 0.5f)] public float grille = 0.06f;
            [Tooltip("Tube edge falloff, fraction of the screen.")]
            [Range(0f, 0.1f)] public float tubeEdge = 0.015f;
        }

        [Serializable]
        public sealed class VignetteSettings
        {
            [Tooltip("Lighter vignette from look 6. Replaces the grade's vignette while on.")]
            public bool enabled;
            [Range(0f, 1f)] public float intensity = 0.18f;
            [Range(0.01f, 1f)] public float smoothness = 0.5f;
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

        [Header("Vector monitor extras (look 6)")]
        [SerializeField] private PhosphorGlowSettings phosphorGlow = new PhosphorGlowSettings();
        [SerializeField] private FringingSettings colourFringing = new FringingSettings();
        [SerializeField, InspectorName("CRT Pass")] private CrtSettings crt = new CrtSettings();
        [SerializeField] private VignetteSettings vignette = new VignetteSettings();

        // ---------------- State ----------------

        private VolumeProfile profile;
        private Tonemapping tonemapping;
        private ColorAdjustments colorAdjust;
        private Bloom bloom;
        private Vignette vignetteFx;
        private FilmGrain grainFx;

        private readonly System.Random random = new System.Random();
        private GameObject atmosphereRoot;
        private Material horizonMaterial, starMaterial, dustMaterial;
        private Mesh horizonMesh;
        private MenuBillboards stars, dust;
        private bool atmosphereDirty = true;

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
            var volume = GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Menu look (generated)";
            profile.hideFlags = HideFlags.HideAndDontSave;
            tonemapping = profile.Add<Tonemapping>();
            colorAdjust = profile.Add<ColorAdjustments>();
            bloom = profile.Add<Bloom>();
            vignetteFx = profile.Add<Vignette>();
            grainFx = profile.Add<FilmGrain>();
            volume.sharedProfile = profile;

            atmosphereDirty = true;
            nextEvent = Time.unscaledTime + MenuGlitchTimeline.NextInterval(random, glitch.interval.x, glitch.interval.y);
            lastInput = Time.unscaledTime;
            Apply(CurrentPreview());
        }

        private void OnDisable()
        {
            if (eventPlaying) EndEvent();
            MenuScreen.Clear();
            var volume = GetComponent<Volume>();
            if (volume != null && volume.sharedProfile == profile) volume.sharedProfile = null;
            Release(profile);
            profile = null;
            DestroyAtmosphere();
            Release(titleMaterial);
            if (titleGlitchTexture != null) titleGlitchTexture.Release();
            Release(titleGlitchTexture);
            titleMaterial = null;
            titleGlitchTexture = null;
        }

        private void OnValidate()
        {
            atmosphereDirty = true;
            if (profile != null) Apply(CurrentPreview());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (atmosphereDirty) RebuildAtmosphere();
            if (!Application.isPlaying)
            {
                Apply(CurrentPreview());
                return;
            }

            float now = Time.unscaledTime;
            TrackInput(now);
            UpdateDust();
            UpdateStatus(now);

            GlitchFrame frameNow = GlitchFrame.Calm;
            if (glitch.holdPeak)
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

        private GlitchFrame CurrentPreview() =>
            glitch.holdPeak ? MenuGlitchTimeline.Frame(MenuGlitchTimeline.PeakFrame) : GlitchFrame.Calm;

        // ---------------- Apply ----------------

        private void Apply(in GlitchFrame f)
        {
            ApplyVolume();
            ApplyScreen(f);
            ApplyInterface(f);
            Color fog = atmosphere.enabled ? atmosphere.haze : Color.black;
            if (RenderSettings.fogColor != fog) RenderSettings.fogColor = fog;
        }

        private void ApplyVolume()
        {
            if (profile == null) return;
            bool g = grade.enabled;
            tonemapping.active = g;
            tonemapping.mode.Override(TonemappingMode.Neutral);
            colorAdjust.active = g;
            colorAdjust.postExposure.Override(grade.exposure);
            colorAdjust.contrast.Override(grade.contrast);

            bool glow = phosphorGlow.enabled;
            bloom.active = glow || (g && grade.bloom);
            bloom.threshold.Override(glow ? phosphorGlow.threshold : grade.bloomThreshold);
            bloom.intensity.Override(glow ? phosphorGlow.intensity : grade.bloomIntensity);
            bloom.scatter.Override(glow ? phosphorGlow.scatter : grade.bloomScatter);
            bloom.highQualityFiltering.Override(true);

            bool lightVignette = vignette.enabled;
            vignetteFx.active = lightVignette || (g && grade.vignette);
            vignetteFx.intensity.Override(lightVignette ? vignette.intensity : grade.vignetteIntensity);
            vignetteFx.smoothness.Override(lightVignette ? vignette.smoothness : grade.vignetteSmoothness);

            grainFx.active = g && grade.grain;
            grainFx.type.Override(grade.grainType);
            grainFx.intensity.Override(grade.grainIntensity);
            grainFx.response.Override(grade.grainResponse);

            if (menuCamera != null)
            {
                var data = menuCamera.GetUniversalAdditionalCameraData();
                bool post = tonemapping.active || bloom.active || vignetteFx.active || grainFx.active;
                if (data.renderPostProcessing != post) data.renderPostProcessing = post;
            }
        }

        private void ApplyScreen(in GlitchFrame f)
        {
            float k = glitch.intensity;
            var glitchVector = new Vector4(glitch.tears ? f.Tear * k : 0f, eventSeed + f.Seed,
                glitch.blocks ? f.Blocks * k : 0f, glitch.scanlines ? f.Scanlines * Mathf.Min(k, 1f) : 0f);
            float eventFringe = glitch.fringe ? f.Fringe * 2f * k : 0f;
            float baseFringe = colourFringing.enabled ? colourFringing.strength : 0f;
            var echo = new Vector4(glitch.echo ? f.EchoPixels * k : 0f, glitch.echo ? f.EchoAmount : 0f,
                Mathf.Max(colourFringing.enabled ? colourFringing.offset : 0f, eventFringe > 0f ? 6f : 0f),
                Mathf.Clamp01(baseFringe + eventFringe));
            var crtVector = crt.enabled ? new Vector4(crt.curvature, crt.scanlines, crt.scanlinePeriod, crt.grille) : Vector4.zero;
            MenuScreen.Set(menuCamera, glitchVector, echo, crtVector, crt.enabled ? crt.tubeEdge : 0f, accent);
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

        // ---------------- Atmosphere ----------------

        private void RebuildAtmosphere()
        {
            atmosphereDirty = false;
            DestroyAtmosphere();
            if (!atmosphere.enabled || menuCamera == null || glowShader == null) return;

            Vector3 eye = menuCamera.transform.position;
            atmosphereRoot = new GameObject("Menu atmosphere (generated)") { hideFlags = HideFlags.HideAndDontSave };
            var seeded = new System.Random(atmosphere.starSeed);

            if (atmosphere.horizon)
            {
                horizonMaterial = GlowMaterial(atmosphere.horizonColor, atmosphere.horizonFalloff, 0f);
                horizonMesh = new Mesh { name = "Menu horizon band", hideFlags = HideFlags.HideAndDontSave };
                Vector3 c = eye + new Vector3(0f, 0f, atmosphere.horizonDistance);
                Vector2 s = atmosphere.horizonSize * 0.5f;
                horizonMesh.vertices = new[]
                {
                    c + new Vector3(-s.x, -s.y), c + new Vector3(s.x, -s.y), c + new Vector3(s.x, s.y), c + new Vector3(-s.x, s.y)
                };
                horizonMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                horizonMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                horizonMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                AddRenderer("Horizon", horizonMesh, horizonMaterial);
            }
            if (atmosphere.stars && atmosphere.starCount > 0)
            {
                starMaterial = GlowMaterial(Color.white * atmosphere.starBrightness, 1.6f, 1f);
                stars = new MenuBillboards("Menu stars", seeded, atmosphere.starCount, atmosphere.starSpread,
                    atmosphere.starHeight, atmosphere.starDepth, atmosphere.starSize, new Vector2(0.1f, 1f), 3f, eye);
                AddRenderer("Stars", stars.Mesh, starMaterial);
            }
            if (atmosphere.dust && atmosphere.dustCount > 0)
            {
                dustMaterial = GlowMaterial(new Color(0.7f, 0.7f, 0.72f) * atmosphere.dustBrightness / 0.55f, 1.2f, 1f);
                dust = new MenuBillboards("Menu dust", seeded, atmosphere.dustCount, atmosphere.dustSpread,
                    atmosphere.dustHeight, atmosphere.dustDepth, atmosphere.dustSize, new Vector2(0.15f, 0.55f), 1f, eye);
                dust.Mesh.MarkDynamic();
                AddRenderer("Dust", dust.Mesh, dustMaterial);
            }
        }

        private void UpdateDust()
        {
            if (dust == null || menuCamera == null || atmosphere.dustDrift <= 0f || Time.deltaTime <= 0f) return;
            Vector3 eye = menuCamera.transform.position;
            float near = eye.z + atmosphere.dustDepth.x, span = atmosphere.dustDepth.y - atmosphere.dustDepth.x;
            float step = atmosphere.dustDrift * Time.deltaTime;
            for (int i = 0; i < dust.Centers.Length; i++)
            {
                dust.Centers[i].z -= step;
                if (dust.Centers[i].z < near) dust.Centers[i].z += span;
            }
            dust.Write(eye);
        }

        private Material GlowMaterial(Color color, float falloff, float mode)
        {
            var m = new Material(glowShader) { hideFlags = HideFlags.HideAndDontSave };
            m.SetColor("_Color", color);
            m.SetFloat("_Power", falloff);
            m.SetFloat("_Mode", mode);
            return m;
        }

        private void AddRenderer(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(atmosphereRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void DestroyAtmosphere()
        {
            Release(atmosphereRoot);
            Release(horizonMesh);
            Release(horizonMaterial);
            Release(starMaterial);
            Release(dustMaterial);
            if (stars != null) Release(stars.Mesh);
            if (dust != null) Release(dust.Mesh);
            atmosphereRoot = null;
            horizonMesh = null;
            horizonMaterial = starMaterial = dustMaterial = null;
            stars = dust = null;
        }

        private static void Release(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}

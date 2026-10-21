using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace AxiomLookDev
{
    /// <summary>Temporary lookdev kit for the main menu: builds candidate looks in Play Mode, captures each
    /// (world and UI rendered separately, the look applied to the world only), then tears everything down.
    /// Nothing here is saved to the scene.</summary>
    public static class LookDev
    {
        public static string OutDir =
            "C:/Users/RS-SYS/AppData/Local/Temp/claude/e--Unity-Projects-Axiom/21056f57-f01a-4c44-9834-bf5bd33b4902/scratchpad/looks/";
        public static bool Dof = false;     // tested and rejected: half-res CoC blurs the 1-2 px wireframes
        const int W = 1920, H = 1080;

        enum Look { None, Cinematic, Atmosphere, Vector, Dither, VectorAtmosphere }

        static readonly List<Object> temp = new List<Object>();
        static readonly List<VisualElement> tempUi = new List<VisualElement>();
        static ChromaticAberration aberration;
        static Label statusLabel;
        static VisualElement focusTick;

        // Capture state shared by one setup
        static Camera cam;
        static UniversalAdditionalCameraData camData;
        static bool camPost;
        static RenderTexture camRT, uiRT;
        static UIDocument[] docs;
        static PanelSettings[] originals;

        /// <summary>which: digits of the shots to take, e.g. "0123457".</summary>
        public static string Capture(string which)
        {
            if (!EditorApplication.isPlaying) return "enter play mode first";
            Directory.CreateDirectory(OutDir);
            Color fog = RenderSettings.fogColor;
            var log = new System.Text.StringBuilder();
            try
            {
                if (which.Contains("0")) log.AppendLine(Shot("0_current", Look.None, null, false, false));
                if (which.Contains("1")) log.AppendLine(Shot("1_cinematic", Look.Cinematic, null, false, false));
                if (which.Contains("2")) log.AppendLine(Shot("2_atmosphere", Look.Atmosphere, null, true, false));
                if (which.Contains("3")) log.AppendLine(Shot("3_vector", Look.Vector, Crt(0.06f, 0.32f, 0.08f, 0.035f), false, false));
                if (which.Contains("4")) log.AppendLine(Shot("4_dither", Look.Dither, "Hidden/AxiomLook/Dither1Bit", false, false));
                if (which.Contains("5")) log.AppendLine(Shot("5_recommended", Look.Atmosphere, null, true, true));
                if (which.Contains("6")) log.AppendLine(Shot("6_vector_atmosphere", Look.VectorAtmosphere, Crt(0.035f, 0.28f, 0.06f, 0.015f), true, true));
                if (which.Contains("7")) log.AppendLine(GlitchSequence("7_glitch"));
            }
            finally
            {
                Teardown();
                RenderSettings.fogColor = fog;
            }
            return log.ToString();
        }

        // ---------------- Setup, one frame, teardown ----------------

        static void Setup(Look look, bool atmosphere, bool uiPolish)
        {
            Teardown();
            BuildVolume(look);
            if (atmosphere) BuildAtmosphere();

            cam = Camera.main;
            camData = cam.GetUniversalAdditionalCameraData();
            camPost = camData.renderPostProcessing;
            camData.renderPostProcessing = look != Look.None && look != Look.Dither;
            camRT = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            uiRT = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = camRT;

            docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            originals = new PanelSettings[docs.Length];
            for (int i = 0; i < docs.Length; i++)
            {
                originals[i] = docs[i].panelSettings;
                var ps = Object.Instantiate(docs[i].panelSettings);
                ps.targetTexture = uiRT;
                ps.clearColor = true;
                ps.colorClearValue = Color.clear;
                docs[i].panelSettings = ps;
                temp.Add(ps);
            }
            if (uiPolish && docs.Length > 0) BuildUiPolish(docs[0].rootVisualElement);
            EditorApplication.isPaused = true;
        }

        /// <summary>Steps the game, then world -> fx passes -> UI on top -> after-UI pass -> JPEG.</summary>
        static void Frame(string file, int steps, Material[] worldFx, Material afterUi)
        {
            for (int i = 0; i < steps; i++) EditorApplication.Step();
            var a = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
            var b = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(camRT, a);
            if (worldFx != null)
                foreach (var fx in worldFx)
                {
                    if (fx == null) continue;
                    Graphics.Blit(a, b, fx);
                    var t = a; a = b; b = t;
                }
            Graphics.Blit(uiRT, a, Mat("Hidden/AxiomLook/AlphaOver"));
            if (afterUi != null)
            {
                Graphics.Blit(a, b, afterUi);
                var t = a; a = b; b = t;
            }
            RenderTexture.active = a;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(OutDir + file + ".jpg", tex.EncodeToJPG(90));
            Object.DestroyImmediate(tex);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }

        static void Teardown()
        {
            if (docs != null)
                for (int i = 0; i < docs.Length; i++)
                    if (docs[i] && originals[i]) docs[i].panelSettings = originals[i];
            docs = null;
            if (cam)
            {
                cam.targetTexture = null;
                if (camData) camData.renderPostProcessing = camPost;
            }
            cam = null;
            if (camRT) camRT.Release();
            if (uiRT) uiRT.Release();
            camRT = uiRT = null;
            foreach (var e in tempUi) e.RemoveFromHierarchy();
            tempUi.Clear();
            foreach (var o in temp) if (o) Object.DestroyImmediate(o);
            temp.Clear();
            aberration = null; statusLabel = null; focusTick = null;
            EditorApplication.isPaused = false;
        }

        static string Shot(string name, Look look, object fx, bool atmosphere, bool uiPolish)
        {
            Setup(look, atmosphere, uiPolish);
            Material m = null;
            if (fx is string s) m = Mat(s);
            else if (fx is CrtSpec c) m = c.Build();
            Frame(name, 30, new[] { m }, null);
            Teardown();
            return name + " ok";
        }

        static Material Mat(string shader)
        {
            var m = new Material(Shader.Find(shader));
            temp.Add(m);
            return m;
        }

        // CRT settings are built after Setup (Setup clears temp objects).
        class CrtSpec
        {
            public float curve, scan, mask, edge;
            public Material Build()
            {
                var m = Mat("Hidden/AxiomLook/VectorCRT");
                m.SetFloat("_Curve", curve); m.SetFloat("_Scan", scan); m.SetFloat("_Mask", mask); m.SetFloat("_Edge", edge);
                return m;
            }
        }
        static CrtSpec Crt(float curve, float scan, float mask, float edge) => new CrtSpec { curve = curve, scan = scan, mask = mask, edge = edge };

        // ---------------- Look 7: glitch event, captured as a frame sequence ----------------

        // One row per captured frame (two game steps each, about 25 fps):
        // tear, seed, echo px, echo amount, blocks, scanlines, roll y (screen 0 top..1 bottom, -1 off),
        // aberration, hero snap x, hero snap y, title tear, title echo px, status scrambled, construct B dropped
        static readonly float[][] Timeline =
        {
            new float[] { 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 2, 0, 0, 0, 0.2f, 0.04f, 0.05f, 0, 0, 0, 0, 0, 0 },
            new float[] { 0.15f, 3, 4, 0.5f, 0, 0.35f, 0.11f, 0.15f, 0, 0, 0, 3, 0, 0 },
            new float[] { 0.4f, 4, 7, 0.8f, 0.2f, 0.5f, 0.18f, 0.25f, 0.3f, 0, 0.35f, 7, 0, 0 },
            new float[] { 0.6f, 5, 11, 0.9f, 0.4f, 0.7f, 0.25f, 0.3f, -0.2f, 0.1f, 0.55f, 10, 1, 1 },
            new float[] { 0.3f, 6, 6, 0.6f, 0.1f, 0.45f, 0.32f, 0.2f, 0, 0, 0.2f, 5, 1, 1 },
            new float[] { 0.12f, 7, 3, 0.4f, 0, 0.3f, 0.39f, 0.1f, 0, 0, 0, 0, 0, 0 },
            new float[] { 0.25f, 8, 5, 0.5f, 0.1f, 0.35f, 0.46f, 0.15f, 0.1f, 0, 0.2f, 4, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0.15f, 0.53f, 0.08f, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0.1f, 0.6f, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0, 0.67f, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0, 0.74f, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0, 0.81f, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0, 0.88f, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 9, 0, 0, 0, 0, 0.95f, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0, 0 },
            new float[] { 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0, 0 },
        };

        static string GlitchSequence(string name)
        {
            Setup(Look.Atmosphere, true, true);
            var world = Mat("Hidden/AxiomLook/Glitch");
            var roll = Mat("Hidden/AxiomLook/Glitch");
            var titleFx = Mat("Hidden/AxiomLook/Glitch");

            var hero = GameObject.Find("Backdrop/HeroConstruct");
            var constructB = GameObject.Find("Backdrop/Construct_B");
            Vector3 heroPos = hero ? hero.transform.position : Vector3.zero;
            var bRenderers = constructB ? constructB.GetComponentsInChildren<Renderer>() : new Renderer[0];

            var root = docs[0].rootVisualElement;
            var title = root.Q("game-title");
            var titleCam = GameObject.Find("MenuTitle/TitleCamera").GetComponent<Camera>();
            var titleBg = new StyleBackground(Background.FromRenderTexture(titleCam.targetTexture));
            var titleGlitch = new RenderTexture(titleCam.targetTexture.width, titleCam.targetTexture.height, 0, RenderTextureFormat.ARGB32);
            temp.Add(titleGlitch);
            string statusText = statusLabel != null ? statusLabel.text : "";

            for (int f = 0; f < Timeline.Length; f++)
            {
                float[] k = Timeline[f];
                world.SetFloat("_Slice", k[0]); world.SetFloat("_Seed", k[1]); world.SetFloat("_Ghost", k[2]);
                world.SetFloat("_GhostAmt", k[3]); world.SetFloat("_Block", k[4]); world.SetFloat("_Scan", k[5]);
                world.SetFloat("_RollY", -1f);
                roll.SetFloat("_RollY", k[6] < 0 ? -1f : 1f - k[6]);      // uv y runs bottom to top
                roll.SetFloat("_RollAmt", 1f);
                if (aberration != null) aberration.intensity.Override(k[7]);
                if (hero) hero.transform.position = heroPos + new Vector3(k[8], k[9], 0f);
                foreach (var r in bRenderers) r.enabled = k[13] < 0.5f;

                if (k[10] > 0f || k[11] > 0f)
                {
                    titleFx.SetFloat("_Slice", k[10]); titleFx.SetFloat("_Seed", k[1] + 20f);
                    titleFx.SetFloat("_Ghost", k[11]); titleFx.SetFloat("_GhostAmt", 0.9f);
                    titleFx.SetFloat("_Scan", k[5] * 0.6f); titleFx.SetFloat("_RollY", -1f);
                    Graphics.Blit(titleCam.targetTexture, titleGlitch, titleFx);
                    title.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(titleGlitch));
                }
                else title.style.backgroundImage = titleBg;
                if (statusLabel != null) statusLabel.text = k[12] > 0.5f ? Scramble(statusText, f) : statusText;
                if (focusTick != null) focusTick.style.opacity = f == 5 ? 0.25f : 1f;

                Frame(name + "_" + f.ToString("00"), f == 0 ? 30 : 2, new[] { world }, roll);
            }

            if (hero) hero.transform.position = heroPos;
            foreach (var r in bRenderers) r.enabled = true;
            title.style.backgroundImage = titleBg;
            if (statusLabel != null) statusLabel.text = statusText;
            Teardown();
            return name + " ok (" + Timeline.Length + " frames)";
        }

        static string Scramble(string s, int seed)
        {
            const string glyphs = "#/\\_-=<>01";
            var r = new System.Random(seed * 31 + 7);
            var c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) if (c[i] != ' ' && r.NextDouble() < 0.45) c[i] = glyphs[r.Next(glyphs.Length)];
            return new string(c);
        }

        // ---------------- Post-processing looks (URP Volume overrides) ----------------

        static void BuildVolume(Look look)
        {
            if (look == Look.None || look == Look.Dither) return;
            var go = new GameObject("LookDev Volume");
            temp.Add(go);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            temp.Add(profile);
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>();
            var vignette = profile.Add<Vignette>();
            var grain = profile.Add<FilmGrain>();
            var grade = profile.Add<ColorAdjustments>();
            bloom.highQualityFiltering.Override(true);
            aberration = profile.Add<ChromaticAberration>();
            aberration.intensity.Override(0f);

            if (look == Look.Vector)
            {
                bloom.threshold.Override(0.35f);
                bloom.intensity.Override(2.4f);
                bloom.scatter.Override(0.65f);
                vignette.intensity.Override(0.45f);
                vignette.smoothness.Override(0.5f);
                grain.type.Override(FilmGrainLookup.Medium1);
                grain.intensity.Override(0.25f);
                grade.contrast.Override(20f);
                aberration.intensity.Override(0.35f);
                return;
            }
            if (look == Look.VectorAtmosphere)
            {
                // Look 3's phosphor glow over look 5's atmosphere, with the vignette cut to about half.
                bloom.threshold.Override(0.45f);
                bloom.intensity.Override(2.0f);
                bloom.scatter.Override(0.65f);
                vignette.intensity.Override(0.18f);
                vignette.smoothness.Override(0.5f);
                grain.type.Override(FilmGrainLookup.Medium1);
                grain.intensity.Override(0.2f);
                grade.contrast.Override(16f);
                grade.postExposure.Override(0.1f);
                aberration.intensity.Override(0.25f);
                return;
            }

            // Cinematic grade (also the base of the atmosphere looks)
            bloom.threshold.Override(0.6f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.7f);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.8f);
            grade.contrast.Override(12f);
            grade.postExposure.Override(0.15f);

            if (look == Look.Atmosphere && Dof)
            {
                var dof = profile.Add<DepthOfField>();
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(22f);
                dof.gaussianEnd.Override(70f);
                dof.gaussianMaxRadius.Override(1.2f);
                dof.highQualitySampling.Override(true);
            }
        }

        // ---------------- Atmosphere: horizon glow, haze, stars, dust ----------------

        static void BuildAtmosphere()
        {
            RenderSettings.fogColor = new Color(0.035f, 0.036f, 0.04f);   // haze, not black: atmospheric perspective

            var band = Mat("AxiomLook/AdditiveGlow");
            band.SetColor("_Color", new Color(0.30f, 0.31f, 0.34f));
            band.SetFloat("_Power", 2.2f);
            band.SetFloat("_Mode", 0f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "LookDev Horizon";
            quad.transform.position = new Vector3(0f, 1.2f, 140f);
            quad.transform.localScale = new Vector3(420f, 34f, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = band;
            temp.Add(quad);

            var dot = Mat("AxiomLook/AdditiveGlow");
            dot.SetColor("_Color", Color.white);
            dot.SetFloat("_Power", 1.6f);
            dot.SetFloat("_Mode", 1f);
            var rng = new System.Random(7);
            temp.Add(Points("LookDev Stars", dot, 700, rng, () => new Vector3(Rand(rng, -170, 170), Rand(rng, 6, 120), Rand(rng, 70, 145)),
                () => Rand(rng, 0.18f, 0.45f), () => Mathf.Pow(Rand(rng, 0f, 1f), 3f) * 0.9f + 0.1f));
            var dust = Mat("AxiomLook/AdditiveGlow");
            dust.SetColor("_Color", new Color(0.7f, 0.7f, 0.72f));
            dust.SetFloat("_Power", 1.2f);
            dust.SetFloat("_Mode", 1f);
            temp.Add(Points("LookDev Dust", dust, 160, rng, () => new Vector3(Rand(rng, -14, 14), Rand(rng, 0.4f, 9f), Rand(rng, 3f, 30f)),
                () => Rand(rng, 0.02f, 0.06f), () => Rand(rng, 0.15f, 0.55f)));
        }

        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static GameObject Points(string name, Material mat, int n, System.Random rng, System.Func<Vector3> pos,
            System.Func<float> size, System.Func<float> bright)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var col = new List<Color>();
            var tri = new List<int>();
            Vector3 eye = Camera.main.transform.position;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = pos();
                float s = size();
                float b = bright();
                Vector3 f = (p - eye).normalized;
                Vector3 r = Vector3.Cross(Vector3.up, f).normalized * s;
                Vector3 u = Vector3.Cross(f, r).normalized * s;
                int k = v.Count;
                v.Add(p - r - u); v.Add(p + r - u); v.Add(p + r + u); v.Add(p - r + u);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                for (int j = 0; j < 4; j++) col.Add(new Color(1, 1, 1, b));
                tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k); tri.Add(k + 3); tri.Add(k + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetColors(col);
            mesh.SetTriangles(tri, 0);
            temp.Add(mesh);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ---------------- UI polish (injected into the live menu, removed afterwards) ----------------

        static void BuildUiPolish(VisualElement root)
        {
            var ink = new Color(0.92f, 0.92f, 0.92f);
            var dim = new Color(0.55f, 0.55f, 0.57f);
            var accent = new Color(0.85f, 0.12f, 0.16f);

            // 1. Scrim: a left-side gradient that lifts the menu column off the busy backdrop.
            var grad = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
            {
                float t = x / 255f;
                float a = 0.78f * (1f - Mathf.SmoothStep(0.25f, 1f, t));
                grad.SetPixel(x, 0, new Color(0, 0, 0, a));
            }
            grad.Apply();
            temp.Add(grad);
            var scrim = Abs(root, 0);
            scrim.style.left = 0; scrim.style.top = 0; scrim.style.bottom = 0;
            scrim.style.width = Length.Percent(62);
            scrim.style.backgroundImage = new StyleBackground(grad);

            // 2. Frame: four corner brackets on a 40 px safe-area inset.
            for (int c = 0; c < 4; c++)
            {
                var b = Abs(root, -1);
                b.style.width = 36; b.style.height = 36;
                bool right = c % 2 == 1, bottom = c >= 2;
                if (right) b.style.right = 40; else b.style.left = 40;
                if (bottom) b.style.bottom = 40; else b.style.top = 40;
                var bc = new Color(ink.r, ink.g, ink.b, 0.35f);
                b.style.borderTopColor = bc; b.style.borderBottomColor = bc; b.style.borderLeftColor = bc; b.style.borderRightColor = bc;
                if (bottom) b.style.borderBottomWidth = 2; else b.style.borderTopWidth = 2;
                if (right) b.style.borderRightWidth = 2; else b.style.borderLeftWidth = 2;
            }

            // 3. Status line on the menu column's grid line (x = 160), small, tracked, low contrast.
            var status = Abs(root, -1);
            status.style.left = 160; status.style.bottom = 56;
            status.style.flexDirection = FlexDirection.Row;
            status.style.alignItems = Align.Center;
            var led = new VisualElement();
            led.style.width = 7; led.style.height = 7; led.style.marginRight = 12;
            led.style.backgroundColor = ink;
            status.Add(led);
            statusLabel = Micro("SYS ONLINE", ink);
            status.Add(statusLabel);
            status.Add(Micro("·", dim));
            status.Add(Micro("COCKPIT LINKED  COM3", dim));
            status.Add(Micro("·", dim));
            status.Add(Micro("BUILD 0.4  PROTOTYPE", dim));

            // 4. Input hints, bottom right: keycaps + verbs.
            var hints = Abs(root, -1);
            hints.style.right = 96; hints.style.bottom = 52;
            hints.style.flexDirection = FlexDirection.Row;
            hints.style.alignItems = Align.Center;
            foreach (var pair in new[] { new[] { "ENTER", "SELECT" }, new[] { "ESC", "BACK" }, new[] { "EMP", "LAUNCH" } })
            {
                var cap = Micro(pair[0], ink);
                cap.style.borderTopWidth = 1; cap.style.borderBottomWidth = 1; cap.style.borderLeftWidth = 1; cap.style.borderRightWidth = 1;
                var cb = new Color(ink.r, ink.g, ink.b, 0.5f);
                cap.style.borderTopColor = cb; cap.style.borderBottomColor = cb; cap.style.borderLeftColor = cb; cap.style.borderRightColor = cb;
                cap.style.borderTopLeftRadius = 3; cap.style.borderTopRightRadius = 3; cap.style.borderBottomLeftRadius = 3; cap.style.borderBottomRightRadius = 3;
                cap.style.paddingLeft = 8; cap.style.paddingRight = 4; cap.style.paddingTop = 3; cap.style.paddingBottom = 3;
                cap.style.marginRight = 10;
                hints.Add(cap);
                var verb = Micro(pair[1], dim);
                verb.style.marginRight = 30;
                hints.Add(verb);
            }

            // 5. Focus: the one red accent marks the selected item (red is reserved for focus and danger).
            var play = root.Q<Button>("play-button");
            if (play != null)
            {
                focusTick = new VisualElement { pickingMode = PickingMode.Ignore };
                focusTick.style.position = Position.Absolute;
                focusTick.style.left = -22; focusTick.style.top = 12; focusTick.style.bottom = 12; focusTick.style.width = 6;
                focusTick.style.backgroundColor = accent;
                play.Add(focusTick);
                tempUi.Add(focusTick);
                play.Focus();
            }
        }

        static VisualElement Abs(VisualElement root, int index)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            if (index >= 0) root.Insert(index, e); else root.Add(e);
            tempUi.Add(e);
            return e;
        }

        static Label Micro(string text, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 14;
            l.style.letterSpacing = 4;
            l.style.color = color;
            l.style.marginRight = 14;
            l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }
    }
}

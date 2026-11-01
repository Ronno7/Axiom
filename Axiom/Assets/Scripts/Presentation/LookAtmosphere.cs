using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>The generated atmosphere around a camera (<see cref="AtmosphereSettings"/>): horizon band, stars and
    /// dust, drawn with the Additive Glow shader, plus the haze (fog and sky share one colour). The horizon and stars
    /// travel with the camera, so they read as infinitely far; dust stays put in the world and wraps around the
    /// camera, so flying through it shows speed. Nothing here is saved.</summary>
    public sealed class LookAtmosphere
    {
        private GameObject root, far;
        private Material horizonMaterial, starMaterial, dustMaterial, cloudMaterial;
        private Mesh horizonMesh, cloudMesh;
        private GlowBillboards stars, dust;
        private Vector3[] dustCourse;
        private float dustShown = 1f;
        private AtmosphereSettings settings;
        private Vector3 builtEye;

        /// <summary>Sets the fog and the camera's background to the haze colour (atmosphere on), or turns the fog off
        /// over a black sky.</summary>
        public static void ApplyHaze(AtmosphereSettings s, Camera camera, float distanceScale = 1f)
        {
            Color sky = s.enabled ? s.haze : Color.black;
            sky.a = 1f;
            if (RenderSettings.fog != s.enabled) RenderSettings.fog = s.enabled;
            if (s.enabled)
            {
                if (RenderSettings.fogMode != FogMode.Linear) RenderSettings.fogMode = FogMode.Linear;
                float scale = Mathf.Max(0.01f, distanceScale);
                float start = Mathf.Max(0f, s.fogRange.x * scale), end = Mathf.Max(start + 1f, s.fogRange.y * scale);
                if (RenderSettings.fogStartDistance != start) RenderSettings.fogStartDistance = start;
                if (RenderSettings.fogEndDistance != end) RenderSettings.fogEndDistance = end;
            }
            if (RenderSettings.fogColor != sky) RenderSettings.fogColor = sky;
            if (camera != null && camera.backgroundColor != sky) camera.backgroundColor = sky;
        }

        /// <summary>Where a dust mote sits along the camera's view once wrapped into the dust's depth range.</summary>
        public static float WrapDepth(float z, float eyeZ, Vector2 depth)
        {
            float near = eyeZ + depth.x, span = depth.y - depth.x;
            return span > 0f ? near + Mathf.Repeat(z - near, span) : z;
        }

        public void Build(AtmosphereSettings s, Camera camera, Shader glowShader, string label, Shader cloudShader = null)
        {
            Dispose();
            settings = s;
            if (!s.enabled || camera == null || glowShader == null) return;

            builtEye = camera.transform.position;
            root = new GameObject(label + " atmosphere (generated)") { hideFlags = HideFlags.HideAndDontSave };
            far = new GameObject("Far") { hideFlags = HideFlags.HideAndDontSave };
            far.transform.SetParent(root.transform, false);
            var seeded = new System.Random(s.starSeed);

            if (s.horizon)
            {
                horizonMaterial = GlowMaterial(glowShader, s.horizonColor, s.horizonFalloff, 0f);
                // Draw over distant surfaces too, so the far floor glows like the sky behind it (no horizon edge).
                horizonMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                horizonMaterial.SetVector("_DepthFade", new Vector4(s.horizonDistance * 0.6f, s.horizonDistance * 0.95f, 0f, 0f));
                horizonMesh = new Mesh { name = label + " horizon band", hideFlags = HideFlags.HideAndDontSave };
                Vector3 c = builtEye + new Vector3(0f, 0f, s.horizonDistance);
                Vector2 h = s.horizonSize * 0.5f;
                horizonMesh.vertices = new[]
                {
                    c + new Vector3(-h.x, -h.y), c + new Vector3(h.x, -h.y), c + new Vector3(h.x, h.y), c + new Vector3(-h.x, h.y)
                };
                horizonMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                horizonMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                horizonMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                AddRenderer(far, "Horizon", horizonMesh, horizonMaterial);
            }
            if (s.clouds && cloudShader != null)
            {
                cloudMaterial = new Material(cloudShader) { hideFlags = HideFlags.HideAndDontSave };
                cloudMaterial.SetVector("_ZenithColor", AxiomPalette.MaterialColor(cloudMaterial, "_ZenithColor", s.haze));
                cloudMaterial.SetVector("_CloudColor", AxiomPalette.MaterialColor(cloudMaterial, "_CloudColor", s.cloudLight));
                cloudMaterial.SetFloat("_Amount", s.cloudCover);
                cloudMaterial.SetFloat("_Brightness", s.cloudBrightness);
                cloudMaterial.SetFloat("_Scale", s.cloudScale);
                cloudMaterial.SetFloat("_Drift", s.cloudDrift);
                cloudMesh = Dome(label + " cloud deck", builtEye, s.cloudRadius);
                AddRenderer(far, "Cloud deck", cloudMesh, cloudMaterial);
            }
            if (s.stars && s.starCount > 0)
            {
                starMaterial = GlowMaterial(glowShader, AxiomPalette.Light, 1.6f, 1f, s.starBrightness);
                stars = new GlowBillboards(label + " stars", seeded, s.starCount, s.starSpread, s.starHeight, s.starDepth,
                    s.starSize, new Vector2(0.1f, 1f), 3f, builtEye);
                AddRenderer(far, "Stars", stars.Mesh, starMaterial);
            }
            if (s.dust && s.dustCount > 0)
            {
                dustMaterial = GlowMaterial(glowShader, AxiomPalette.Text, 1.2f, 1f, s.dustBrightness / 0.55f);
                dust = new GlowBillboards(label + " dust", seeded, s.dustCount, s.dustSpread, s.dustHeight, s.dustDepth,
                    s.dustSize, new Vector2(0.15f, 0.55f), 1f, builtEye);
                dust.Mesh.MarkDynamic();
                AddRenderer(root, "Dust", dust.Mesh, dustMaterial);
                // Mostly sideways, a little up or down: motes hang in the air rather than stream.
                dustCourse = new Vector3[s.dustCount];
                for (int i = 0; i < dustCourse.Length; i++)
                {
                    var course = new Vector3((float)seeded.NextDouble() * 2f - 1f, (float)seeded.NextDouble() - 0.4f,
                        (float)seeded.NextDouble() - 0.5f);
                    dustCourse[i] = course.normalized * (s.dustFloat * (0.5f + 0.5f * (float)seeded.NextDouble()));
                }
                dustShown = -1f;
                SetDustVisibility(1f);
            }
        }

        /// <summary>Keeps the horizon and stars with the camera and moves the dust: drift toward a still camera
        /// (<paramref name="deltaTime"/>), each mote's own float (<paramref name="floatTime"/>), then wrap.</summary>
        public void Follow(Camera camera, float deltaTime, float floatTime = 0f)
        {
            if (root == null || camera == null) return;
            Vector3 eye = camera.transform.position;
            far.transform.position = eye - builtEye;
            if (dust == null) return;

            float step = settings.dustDrift * deltaTime;
            float spread = settings.dustSpread;
            Vector2 height = settings.dustHeight;
            for (int i = 0; i < dust.Centers.Length; i++)
            {
                Vector3 c = dust.Centers[i] + dustCourse[i] * floatTime;
                c.z = WrapDepth(c.z - step, eye.z, settings.dustDepth);
                if (floatTime > 0f)
                {
                    c.x = eye.x - spread + Mathf.Repeat(c.x - (eye.x - spread), spread * 2f);
                    if (height.y > height.x) c.y = height.x + Mathf.Repeat(c.y - height.x, height.y - height.x);
                }
                dust.Centers[i] = c;
            }
            dust.Write(eye);
        }

        /// <summary>Scales the dust's glow (0 hides it). Survives a rebuild only if called again.</summary>
        public void SetDustVisibility(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (dustMaterial == null || Mathf.Approximately(amount, dustShown)) return;
            dustShown = amount;
            dustMaterial.SetVector("_Color", AxiomPalette.MaterialColor(dustMaterial, "_Color", AxiomPalette.Text,
                settings.dustBrightness / 0.55f * amount, AxiomPalette.Text.a));
        }

        public void Dispose()
        {
            Generated.Release(root);
            Generated.Release(horizonMesh);
            Generated.Release(horizonMaterial);
            Generated.Release(starMaterial);
            Generated.Release(dustMaterial);
            Generated.Release(cloudMaterial);
            Generated.Release(cloudMesh);
            if (stars != null) Generated.Release(stars.Mesh);
            if (dust != null) Generated.Release(dust.Mesh);
            root = far = null;
            horizonMesh = cloudMesh = null;
            horizonMaterial = starMaterial = dustMaterial = cloudMaterial = null;
            stars = dust = null;
            dustCourse = null;
        }

        /// <summary>An upper dome (a little below the horizon too) around <paramref name="center"/>.</summary>
        private static Mesh Dome(string meshName, Vector3 center, float radius)
        {
            const int around = 48, rings = 14;
            const float lowest = -12f;
            var vertices = new Vector3[(around + 1) * (rings + 1)];
            var triangles = new int[around * rings * 6];
            for (int r = 0; r <= rings; r++)
            {
                float elevation = Mathf.Lerp(lowest, 90f, r / (float)rings) * Mathf.Deg2Rad;
                for (int a = 0; a <= around; a++)
                {
                    float azimuth = a / (float)around * Mathf.PI * 2f;
                    vertices[r * (around + 1) + a] = center + radius * new Vector3(
                        Mathf.Cos(elevation) * Mathf.Sin(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Cos(azimuth));
                }
            }
            int t = 0;
            for (int r = 0; r < rings; r++)
                for (int a = 0; a < around; a++)
                {
                    int i = r * (around + 1) + a, up = i + around + 1;
                    triangles[t++] = i; triangles[t++] = up; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = up; triangles[t++] = up + 1;
                }
            var mesh = new Mesh { name = meshName, hideFlags = HideFlags.HideAndDontSave, vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Material GlowMaterial(Shader shader, Color color, float falloff, float mode, float intensity = 1f)
        {
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m.SetVector("_Color", AxiomPalette.MaterialColor(m, "_Color", color, intensity, color.a));
            m.SetFloat("_Power", falloff);
            m.SetFloat("_Mode", mode);
            return m;
        }

        private static void AddRenderer(GameObject parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}

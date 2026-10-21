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
        private Material horizonMaterial, starMaterial, dustMaterial;
        private Mesh horizonMesh;
        private GlowBillboards stars, dust;
        private AtmosphereSettings settings;
        private Vector3 builtEye;

        /// <summary>Sets the fog and the camera's background to the haze colour (atmosphere on), or turns the fog off
        /// over a black sky.</summary>
        public static void ApplyHaze(AtmosphereSettings s, Camera camera)
        {
            Color sky = s.enabled ? s.haze : Color.black;
            sky.a = 1f;
            if (RenderSettings.fog != s.enabled) RenderSettings.fog = s.enabled;
            if (s.enabled)
            {
                if (RenderSettings.fogMode != FogMode.Linear) RenderSettings.fogMode = FogMode.Linear;
                float start = Mathf.Max(0f, s.fogRange.x), end = Mathf.Max(start + 1f, s.fogRange.y);
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

        public void Build(AtmosphereSettings s, Camera camera, Shader glowShader, string label)
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
            if (s.stars && s.starCount > 0)
            {
                starMaterial = GlowMaterial(glowShader, Color.white * s.starBrightness, 1.6f, 1f);
                stars = new GlowBillboards(label + " stars", seeded, s.starCount, s.starSpread, s.starHeight, s.starDepth,
                    s.starSize, new Vector2(0.1f, 1f), 3f, builtEye);
                AddRenderer(far, "Stars", stars.Mesh, starMaterial);
            }
            if (s.dust && s.dustCount > 0)
            {
                dustMaterial = GlowMaterial(glowShader, new Color(0.7f, 0.7f, 0.72f) * s.dustBrightness / 0.55f, 1.2f, 1f);
                dust = new GlowBillboards(label + " dust", seeded, s.dustCount, s.dustSpread, s.dustHeight, s.dustDepth,
                    s.dustSize, new Vector2(0.15f, 0.55f), 1f, builtEye);
                dust.Mesh.MarkDynamic();
                AddRenderer(root, "Dust", dust.Mesh, dustMaterial);
            }
        }

        /// <summary>Keeps the horizon and stars with the camera and moves the dust (drift, then wrap).</summary>
        public void Follow(Camera camera, float deltaTime)
        {
            if (root == null || camera == null) return;
            Vector3 eye = camera.transform.position;
            far.transform.position = eye - builtEye;
            if (dust == null) return;

            float step = settings.dustDrift * deltaTime;
            for (int i = 0; i < dust.Centers.Length; i++)
                dust.Centers[i].z = WrapDepth(dust.Centers[i].z - step, eye.z, settings.dustDepth);
            dust.Write(eye);
        }

        public void Dispose()
        {
            Generated.Release(root);
            Generated.Release(horizonMesh);
            Generated.Release(horizonMaterial);
            Generated.Release(starMaterial);
            Generated.Release(dustMaterial);
            if (stars != null) Generated.Release(stars.Mesh);
            if (dust != null) Generated.Release(dust.Mesh);
            root = far = null;
            horizonMesh = null;
            horizonMaterial = starMaterial = dustMaterial = null;
            stars = dust = null;
        }

        private static Material GlowMaterial(Shader shader, Color color, float falloff, float mode)
        {
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m.SetColor("_Color", color);
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

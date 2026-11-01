using GeometricFlight.Combat;
using GeometricFlight.Input;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>A dominant planar shock ring, with lighter spherical arcs and radial energy fragments.
    /// All fronts read the gameplay clock, and stay at the ship's position/orientation at discharge.</summary>
    [RequireComponent(typeof(EmpWeapon))]
    public sealed class EmpDisplay : MonoBehaviour
    {
        [SerializeField] private Material pulseMaterial;
        private EmpWeapon weapon;
        private MasterArmInput authority;
        private Transform shell;
        private readonly LineRenderer[] rings = new LineRenderer[6];
        private readonly LineRenderer[] fragments = new LineRenderer[32];
        private readonly Vector3[] directions = new Vector3[32];
        private MaterialPropertyBlock properties;
        private AudioSource sound;
        private AudioClip impact;
        private bool impactPlayed;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            weapon = GetComponent<EmpWeapon>();
            authority = GetComponent<MasterArmInput>();
            weapon.Detonated += Detonate;
            shell = new GameObject("EMP Shock Front").transform;
            properties = new MaterialPropertyBlock();
            for (int r = 0; r < rings.Length; r++)
            {
                rings[r] = MakeLine(r < 2 ? "Main Ring " + r : "Secondary Arc " + r, r < 2 ? 160 : 48, r < 2);
                rings[r].transform.localRotation = r < 2 ? Quaternion.Euler(90, 0, 0)
                    : Quaternion.Euler(20 + (r - 2) * 43, (r - 2) * 71, (r - 2) * 37);
            }
            // Even coverage of the sphere, rather than another pair of equally bright crossed hoops.
            for (int i = 0; i < fragments.Length; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / fragments.Length;
                float a = i * 2.399963f;
                float radial = Mathf.Sqrt(1f - y * y);
                directions[i] = new Vector3(Mathf.Cos(a) * radial, y, Mathf.Sin(a) * radial);
                fragments[i] = MakeLine("Energy Fragment " + i, 2, false);
            }
            shell.gameObject.SetActive(false);
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.volume = 0.65f;
            // Original metallic bass impact with a falling overtone and long tail; generated once.
            const int rate = 24000;
            var samples = new float[rate * 3];
            var random = new System.Random(47);
            float phase = 0f, overtone = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                phase += 2f * Mathf.PI * (42f + 65f * Mathf.Exp(-t * 7f)) / rate;
                overtone += 2f * Mathf.PI * (95f + 650f * Mathf.Exp(-t * 5f)) / rate;
                float envelope = Mathf.Min(1f, t * 180f) * Mathf.Exp(-t * 1.7f);
                float metal = Mathf.Sin(overtone + 1.8f * Mathf.Sin(overtone * 1.4142f));
                float noise = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 24f);
                samples[i] = envelope * (Mathf.Sin(phase) * 0.55f + metal * 0.25f + noise * 0.15f);
            }
            impact = AudioClip.Create("EMP Impact", samples.Length, 1, rate, false);
            impact.SetData(samples, 0);
        }

        private void Detonate()
        {
            shell.SetPositionAndRotation(weapon.Origin, weapon.Orientation);
            impactPlayed = false;
            sound.Stop();
            ControllerService.Current?.Display.Flash(LcdGlyph.Center("EMP DISCHARGED"),
                LcdGlyph.Center("CHARGE SPENT"), Time.unscaledTimeAsDouble + 3.5);
        }

        private LineRenderer MakeLine(string label, int points, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(shell, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = pulseMaterial;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private void SetLight(LineRenderer line, float intensity)
        {
            // Raw shader values keep HDR brightness independent of opacity and avoid a second sRGB conversion.
            properties.SetVector(BaseColorId, AxiomPalette.ShaderColor(AxiomPalette.Cyan, intensity));
            line.SetPropertyBlock(properties);
        }

        private void LateUpdate()
        {
            // One owner for the physical lamp: a ready charge also needs weapons authority.
            bool available = authority == null ||
                (authority.WeaponsAvailable && authority.Armed && Time.timeScale > 0f);
            ControllerService.Current?.SetEmpLamp(weapon.State.Ready && available
                ? ControllerProtocol.Lamp.On : ControllerProtocol.Lamp.Off);
            shell.gameObject.SetActive(weapon.Pulsing);
            if (!weapon.Pulsing) return;
            // The audible/physical impact follows compression, synchronized to the world clock.
            if (!weapon.Compressing && !impactPlayed && Time.timeScale > 0f)
            {
                impactPlayed = true;
                sound.PlayOneShot(impact);
                ControllerService.Current?.PlayTone(85, 900);
                ControllerService.Current?.PulseRumble(230, 900);
            }
            float radius = weapon.Radius * weapon.Progress;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, weapon.Progress));
            for (int r = 0; r < rings.Length; r++)
            {
                LineRenderer line = rings[r];
                line.enabled = !weapon.Compressing || r == 0;
                float ringRadius = weapon.Compressing ? Mathf.Lerp(7f, 0.7f, weapon.CompressionProgress)
                    : radius * (r == 1 ? 0.965f : 1f);
                line.widthMultiplier = (r == 0 ? 1.2f : r == 1 ? 0.35f : 0.32f) * fade;
                SetLight(line, (r == 0 ? 4f : r == 1 ? 1.3f : 0.85f) * fade);
                for (int p = 0; p < line.positionCount; p++)
                {
                    float a = r < 2 ? p * Mathf.PI * 2f / line.positionCount
                        : (r * 1.7f + p / (float)(line.positionCount - 1) * 1.9f);
                    line.SetPosition(p, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius);
                }
            }
            for (int i = 0; i < fragments.Length; i++)
            {
                LineRenderer line = fragments[i];
                line.enabled = !weapon.Compressing;
                line.widthMultiplier = 0.16f * fade;
                SetLight(line, 1.4f * fade);
                float tail = Mathf.Min(radius, 5f + (i % 5) * 1.5f);
                line.SetPosition(0, directions[i] * (radius - tail));
                line.SetPosition(1, directions[i] * radius);
            }
        }

        private void OnDisable()
        {
            if (shell != null) shell.gameObject.SetActive(false);
            if (sound != null) sound.Stop();
            ControllerService.Current?.SetEmpLamp(ControllerProtocol.Lamp.Off);
        }

        private void OnDestroy()
        {
            if (weapon != null) weapon.Detonated -= Detonate;
            if (shell != null) Destroy(shell.gameObject);
            if (impact != null) Destroy(impact);
            if (sound != null) Destroy(sound);
        }
    }
}

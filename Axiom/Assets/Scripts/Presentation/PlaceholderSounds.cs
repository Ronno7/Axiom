using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Placeholder sounds generated in code until real sound design replaces them: the ship's
    /// impact when it is destroyed and the rising tone of its rebuild. Each clip is made once, on first use.
    /// </summary>
    public static class PlaceholderSounds
    {
        private const int Rate = 24000;
        private static AudioClip impact, build;

        /// <summary>A low thump under a short burst of noise, about 0.45 s.</summary>
        public static AudioClip ShipImpact => impact != null ? impact : impact = MakeImpact();

        /// <summary>A soft tone rising over the rebuild (0.75 s to 2.5 s after the crash).</summary>
        public static AudioClip ShipBuild => build != null ? build : build = MakeBuild(1.75f);

        private static AudioClip MakeImpact()
        {
            var samples = new float[(int)(Rate * 0.45f)];
            var random = new System.Random(11);
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                phase += 2f * Mathf.PI * Mathf.Lerp(70f, 38f, t / 0.45f) / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 9f);
                float noise = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 28f);
                float crack = Mathf.Sin(2f * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 90f);
                samples[i] = 0.62f * thump + 0.32f * noise + 0.18f * crack;
            }
            return Clip("Ship impact (placeholder)", samples);
        }

        private static AudioClip MakeBuild(float seconds)
        {
            var samples = new float[(int)(Rate * seconds)];
            float a = 0f, b = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate, u = t / seconds;
                float pitch = Mathf.Lerp(196f, 392f, u * u);
                a += 2f * Mathf.PI * pitch / Rate;
                b += 2f * Mathf.PI * pitch * 1.5f / Rate;
                float envelope = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((seconds - t) / 0.12f);
                float shimmer = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                samples[i] = envelope * shimmer * (0.32f * Mathf.Sin(a) + 0.12f * Mathf.Sin(b));
            }
            return Clip("Ship rebuild (placeholder)", samples);
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

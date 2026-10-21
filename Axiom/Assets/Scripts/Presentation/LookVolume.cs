using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Presentation
{
    /// <summary>A generated volume profile holding a scene's grade (<see cref="GradeSettings"/>). While Arcade Mode is
    /// on, its glow, vignette and grain replace the grade's and its colour adjustments add to them. Turns the camera's
    /// post-processing on only while an effect needs it. The profile is never saved.</summary>
    public sealed class LookVolume
    {
        public readonly VolumeProfile Profile;
        private readonly Volume volume;
        private readonly Tonemapping tonemapping;
        private readonly ColorAdjustments colour;
        private readonly WhiteBalance whiteBalance;
        private readonly Bloom bloom;
        private readonly Vignette vignette;
        private readonly FilmGrain grain;

        public LookVolume(Volume volume, string name)
        {
            this.volume = volume;
            volume.isGlobal = true;
            volume.priority = 10f;
            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Profile.name = name;
            Profile.hideFlags = HideFlags.HideAndDontSave;
            tonemapping = Profile.Add<Tonemapping>();
            colour = Profile.Add<ColorAdjustments>();
            whiteBalance = Profile.Add<WhiteBalance>();
            bloom = Profile.Add<Bloom>();
            vignette = Profile.Add<Vignette>();
            grain = Profile.Add<FilmGrain>();
            volume.sharedProfile = Profile;
        }

        /// <param name="arcade">Arcade Mode's look while it's on, otherwise null.</param>
        public void Apply(GradeSettings grade, ArcadeProfile arcade, Camera camera)
        {
            bool g = grade.enabled;
            bool arcadeColour = arcade != null && arcade.colour;
            tonemapping.active = g;
            tonemapping.mode.Override(TonemappingMode.Neutral);

            colour.active = g || arcadeColour;
            colour.postExposure.Override((g ? grade.exposure : 0f) + (arcadeColour ? arcade.exposure : 0f));
            colour.contrast.Override(Mathf.Clamp((g ? grade.contrast : 0f) + (arcadeColour ? arcade.contrast : 0f), -100f, 100f));
            colour.saturation.Override(arcadeColour ? arcade.saturation : 0f);
            colour.colorFilter.Override(arcadeColour ? arcade.colourFilter : Color.white);
            colour.hueShift.Override(arcadeColour ? arcade.hueShift : 0f);
            whiteBalance.active = arcadeColour;
            whiteBalance.temperature.Override(arcadeColour ? arcade.temperature : 0f);
            whiteBalance.tint.Override(arcadeColour ? arcade.tint : 0f);

            bool glow = arcade != null && arcade.glow;
            bloom.active = glow || (g && grade.bloom);
            bloom.threshold.Override(glow ? arcade.glowThreshold : grade.bloomThreshold);
            bloom.intensity.Override(glow ? arcade.glowIntensity : grade.bloomIntensity);
            bloom.scatter.Override(glow ? arcade.glowScatter : grade.bloomScatter);
            bloom.highQualityFiltering.Override(true);

            bool arcadeVignette = arcade != null && arcade.vignette;
            vignette.active = arcadeVignette || (g && grade.vignette);
            vignette.intensity.Override(arcadeVignette ? arcade.vignetteIntensity : grade.vignetteIntensity);
            vignette.smoothness.Override(arcadeVignette ? arcade.vignetteSmoothness : grade.vignetteSmoothness);

            bool arcadeGrain = arcade != null && arcade.extras && arcade.grain;
            grain.active = arcadeGrain || (g && grade.grain);
            grain.type.Override(arcadeGrain ? arcade.grainType : grade.grainType);
            grain.intensity.Override(arcadeGrain ? arcade.grainIntensity : grade.grainIntensity);
            grain.response.Override(grade.grainResponse);

            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                bool post = tonemapping.active || colour.active || bloom.active || vignette.active || grain.active;
                if (data.renderPostProcessing != post) data.renderPostProcessing = post;
            }
        }

        public void Dispose()
        {
            if (volume != null && volume.sharedProfile == Profile) volume.sharedProfile = null;
            Generated.Release(Profile);
        }
    }
}

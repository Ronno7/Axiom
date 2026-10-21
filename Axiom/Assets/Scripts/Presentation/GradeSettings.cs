using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Presentation
{
    /// <summary>A scene's colour grade (tonemapping, exposure, contrast, bloom, vignette, grain), applied by
    /// <see cref="LookVolume"/>. Used by <see cref="MenuLook"/> and <see cref="LevelLook"/>.</summary>
    [Serializable]
    public sealed class GradeSettings
    {
        [Tooltip("Tonemapping, exposure, contrast, bloom, vignette and grain on the camera.")]
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
}

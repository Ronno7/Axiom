using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GeometricFlight.Presentation
{
    /// <summary>The look of Arcade Mode (Options): a vintage vector monitor. One asset shared by the menu
    /// (<see cref="MenuLook"/>) and the levels (<see cref="LevelLook"/>), so it's tuned in one place.
    /// Each part can be switched off on its own.</summary>
    [CreateAssetMenu(menuName = "Axiom/Arcade Look", fileName = "ArcadeLook")]
    public sealed class ArcadeProfile : ScriptableObject
    {
        [Header("Phosphor glow")]
        [Tooltip("Strong bloom, like lit phosphor. Replaces the normal bloom while Arcade Mode is on.")]
        public bool glow = true;
        [Range(0f, 2f)] public float glowThreshold = 0.45f;
        [Range(0f, 5f)] public float glowIntensity = 2f;
        [Range(0f, 1f)] public float glowScatter = 0.65f;

        [Header("Colour fringing")]
        [Tooltip("Lens-style fringing in the accent red only, so the palette stays black, white and red.")]
        public bool fringing = true;
        [Tooltip("Offset at the screen edge, in pixels; zero at the centre.")]
        [Range(0f, 12f)] public float fringeOffset = 2.97f;
        [Range(0f, 1f)] public float fringeStrength = 1f;

        [Header("CRT")]
        [Tooltip("Tube curvature, scanlines, a monochrome aperture grille and darkened tube edges.")]
        public bool crt = true;
        [Range(0f, 0.3f)] public float curvature = 0.035f;
        [Range(0f, 1f)] public float scanlines = 0.28f;
        [Tooltip("Scanline period in pixels.")]
        [Range(2f, 8f)] public float scanlinePeriod = 3f;
        [Range(0f, 0.5f)] public float grille = 0.06f;
        [Tooltip("Tube edge falloff, fraction of the screen.")]
        [Range(0f, 0.1f)] public float tubeEdge = 0.015f;

        [Header("Colour")]
        [Tooltip("Colour adjustments on top of the scene's normal grade. Exposure and contrast add to the grade's.")]
        public bool colour = true;
        [Tooltip("Added to the grade's exposure, in stops.")]
        [Range(-2f, 2f)] public float exposure;
        [Tooltip("Added to the grade's contrast.")]
        [Range(-100f, 100f)] public float contrast = 10f;
        [Tooltip("The scene is mostly black and white, so this mainly sets how hard the accent red burns.")]
        [Range(-100f, 100f)] public float saturation = 15f;
        [Tooltip("Multiplies the picture; white leaves it unchanged.")]
        [ColorUsage(false, true)] public Color colourFilter = Color.white;
        [Tooltip("Moves every hue, the accent red included.")]
        [Range(-180f, 180f)] public float hueShift;
        [Tooltip("White balance: negative is cooler (bluish whites, like an old 9300 K monitor), positive warmer.")]
        [Range(-100f, 100f)] public float temperature;
        [Tooltip("White balance: negative is greener, positive more magenta.")]
        [Range(-100f, 100f)] public float tint;

        [Header("Vignette")]
        [Tooltip("Replaces the normal vignette while Arcade Mode is on.")]
        public bool vignette = true;
        [Range(0f, 1f)] public float vignetteIntensity = 0.18f;
        [Range(0.01f, 1f)] public float vignetteSmoothness = 0.5f;

        [Header("Vintage extras")]
        public bool extras = true;
        [Tooltip("Rounded tube corners, as a fraction of the half-screen.")]
        [Range(0f, 0.3f)] public float cornerRadius = 0.06f;
        [Tooltip("Faint brightness flicker.")]
        [Range(0f, 0.1f)] public float flicker = 0.015f;
        [Tooltip("Fine static on the picture.")]
        [Range(0f, 0.15f)] public float staticNoise = 0.025f;
        [Tooltip("A faint brighter band rolling slowly down the screen.")]
        [Range(0f, 0.2f)] public float rollingBand = 0.05f;
        [Tooltip("Coarser film grain while Arcade Mode is on.")]
        public bool grain = true;
        public FilmGrainLookup grainType = FilmGrainLookup.Medium1;
        [Range(0f, 1f)] public float grainIntensity = 0.3f;

        public Vector4 CrtVector => crt ? new Vector4(curvature, scanlines, scanlinePeriod, grille) : Vector4.zero;
        public float TubeEdge => crt ? tubeEdge : 0f;
        public float FringeOffset => fringing ? fringeOffset : 0f;
        public float FringeStrength => fringing ? fringeStrength : 0f;
        public Vector4 ExtrasVector =>
            extras ? new Vector4(cornerRadius, flicker, staticNoise, rollingBand) : Vector4.zero;
    }
}

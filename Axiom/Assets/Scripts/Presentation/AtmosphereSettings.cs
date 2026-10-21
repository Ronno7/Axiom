using System;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>A scene's atmosphere (haze, horizon band, stars, dust), built by <see cref="LookAtmosphere"/>.
    /// Used by <see cref="MenuLook"/> and <see cref="LevelLook"/>. Distances are from the camera; keep them inside
    /// its far clip plane.</summary>
    [Serializable]
    public sealed class AtmosphereSettings
    {
        [Tooltip("Haze, horizon band, stars and dust.")]
        public bool enabled = true;
        [Tooltip("Fog and sky colour while on: a dark haze instead of black, so distance softens objects instead of "
               + "erasing them, and the floor fades into the sky with no edge.")]
        public Color haze = new Color(0.035f, 0.036f, 0.04f);
        [Tooltip("Linear fog: where the haze starts and where it covers everything, in metres.")]
        public Vector2 fogRange = new Vector2(35f, 135f);
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
        [Tooltip("Distance range in front of the camera, in metres. Dust stays put in the world and wraps around "
               + "this range as the camera moves.")]
        public Vector2 dustDepth = new Vector2(3f, 30f);
        public Vector2 dustSize = new Vector2(0.02f, 0.06f);
        [Range(0f, 2f)] public float dustBrightness = 0.55f;
        [Tooltip("Metres per second toward the camera, for a still camera (the menu: 4 matches the floor markers). "
               + "Leave at 0 where the camera flies.")]
        [Min(0f)] public float dustDrift = 4f;
    }
}

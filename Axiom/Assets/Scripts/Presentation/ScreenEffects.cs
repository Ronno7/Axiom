using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Link between the components that drive screen effects (<see cref="MenuLook"/> on the menu,
    /// <see cref="LevelLook"/> in levels) and <see cref="ScreenEffectsFeature"/>: which camera gets the pass and
    /// the shader globals it reads. Globals rather than a material, so nothing is saved to an asset;
    /// <see cref="Clear"/> runs when the driving component goes away, so other scenes never see the pass.</summary>
    public static class ScreenEffects
    {
        private static readonly int GlitchId = Shader.PropertyToID("_ScreenGlitch");
        private static readonly int EchoId = Shader.PropertyToID("_ScreenEcho");
        private static readonly int CrtId = Shader.PropertyToID("_ScreenCrt");
        private static readonly int CrtEdgeId = Shader.PropertyToID("_ScreenCrtEdge");
        private static readonly int ArcadeId = Shader.PropertyToID("_ScreenArcade");
        private static readonly int AccentId = Shader.PropertyToID("_ScreenAccent");

        public static Camera Camera { get; private set; }
        public static bool Active { get; private set; }

        public static bool ShouldRender(Camera camera) => Active && camera != null && camera == Camera;

        /// <param name="glitch">x tear amount, y seed, z blocks, w event scanlines</param>
        /// <param name="echo">x glitch echo (px), y glitch echo amount, z fringe (px at the edge), w fringe amount</param>
        /// <param name="crt">x curvature, y scanline strength, z scanline period (px), w grille</param>
        /// <param name="arcade">x rounded tube corners, y flicker, z static noise, w rolling band</param>
        public static void Set(Camera camera, Vector4 glitch, Vector4 echo, Vector4 crt, float crtEdge, Vector4 arcade,
            Color accent)
        {
            Camera = camera;
            Active = camera != null && (glitch.x > 0f || glitch.z > 0f || glitch.w > 0f || echo.y > 0f || echo.w > 0f
                                        || crt.x > 0f || crt.y > 0f || crt.w > 0f || crtEdge > 0f
                                        || arcade.x > 0f || arcade.y > 0f || arcade.z > 0f || arcade.w > 0f);
            Shader.SetGlobalVector(GlitchId, glitch);
            Shader.SetGlobalVector(EchoId, echo);
            Shader.SetGlobalVector(CrtId, crt);
            Shader.SetGlobalFloat(CrtEdgeId, crtEdge);
            Shader.SetGlobalVector(ArcadeId, arcade);
            Shader.SetGlobalColor(AccentId, accent);
        }

        public static void Clear()
        {
            Set(null, Vector4.zero, Vector4.zero, Vector4.zero, 0f, Vector4.zero, Color.black);
        }
    }
}

using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Link between <see cref="MenuLook"/> and <see cref="MenuScreenFeature"/>: which camera gets the menu
    /// screen pass and the shader globals it reads. Globals rather than a material, so nothing is saved to an asset;
    /// <see cref="Clear"/> runs when the menu look goes away, so other scenes never see the pass.</summary>
    public static class MenuScreen
    {
        private static readonly int GlitchId = Shader.PropertyToID("_MenuGlitch");
        private static readonly int EchoId = Shader.PropertyToID("_MenuEcho");
        private static readonly int CrtId = Shader.PropertyToID("_MenuCrt");
        private static readonly int CrtEdgeId = Shader.PropertyToID("_MenuCrtEdge");
        private static readonly int AccentId = Shader.PropertyToID("_MenuAccent");

        public static Camera Camera { get; private set; }
        public static bool Active { get; private set; }

        public static bool ShouldRender(Camera camera) => Active && camera != null && camera == Camera;

        /// <param name="glitch">x tear amount, y seed, z blocks, w event scanlines</param>
        /// <param name="echo">x glitch echo (px), y glitch echo amount, z fringe (px at the edge), w fringe amount</param>
        /// <param name="crt">x curvature, y scanline strength, z scanline period (px), w grille</param>
        public static void Set(Camera camera, Vector4 glitch, Vector4 echo, Vector4 crt, float crtEdge, Color accent)
        {
            Camera = camera;
            Active = camera != null && (glitch.x > 0f || glitch.z > 0f || glitch.w > 0f || echo.y > 0f || echo.w > 0f
                                        || crt.x > 0f || crt.y > 0f || crt.w > 0f || crtEdge > 0f);
            Shader.SetGlobalVector(GlitchId, glitch);
            Shader.SetGlobalVector(EchoId, echo);
            Shader.SetGlobalVector(CrtId, crt);
            Shader.SetGlobalFloat(CrtEdgeId, crtEdge);
            Shader.SetGlobalColor(AccentId, accent);
        }

        public static void Clear()
        {
            Set(null, Vector4.zero, Vector4.zero, Vector4.zero, 0f, Color.black);
        }
    }
}

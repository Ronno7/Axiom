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

        private static readonly int InterfaceId = Shader.PropertyToID("_ScreenInterface");
        private static readonly int InterfaceLookId = Shader.PropertyToID("_ScreenInterfaceLook");

        public static Camera Camera { get; private set; }
        public static bool Active { get; private set; }

        /// <summary>The camera whose picture the interface is drawn into (Arcade Mode), or null.</summary>
        public static Camera InterfaceCamera { get; private set; }

        public static bool ShouldRender(Camera camera) => Active && camera != null && camera == Camera;

        public static bool ShouldComposite(Camera camera) => camera != null && camera == InterfaceCamera;

        /// <summary>Draws the interface (rendered into <paramref name="ui"/>) into this camera's picture just before
        /// the screen effects, with a soft phosphor glow, so the CRT pass bends, scans and fringes it too.</summary>
        /// <param name="glow">Glow strength (0 = none) and radius in pixels at 1080p.</param>
        public static void SetInterface(Camera camera, RenderTexture ui, float brightness, Vector2 glow)
        {
            InterfaceCamera = ui != null ? camera : null;
            Shader.SetGlobalTexture(InterfaceId, ui != null ? ui : Texture2D.blackTexture);
            Shader.SetGlobalVector(InterfaceLookId, new Vector4(brightness, glow.x, glow.y, 0f));
        }

        public static void ClearInterface() => SetInterface(null, null, 1f, Vector2.zero);

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
            // The effects read the interface's coverage; with none routed it must be clear, not an unset (white) default.
            if (InterfaceCamera == null) Shader.SetGlobalTexture(InterfaceId, Texture2D.blackTexture);
        }

        public static void Clear()
        {
            Set(null, Vector4.zero, Vector4.zero, Vector4.zero, 0f, Vector4.zero, Color.black);
        }
    }
}

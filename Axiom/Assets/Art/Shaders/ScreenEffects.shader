// Screen effects, run by ScreenEffectsFeature after URP post-processing, on the registered camera only.
// Glitch (tears, blocks, red echo, event scanlines), palette fringing (a radial echo in the accent red, so the
// palette stays black, white and red), a CRT pass (curvature, scanlines, monochrome grille, tube edge) and
// arcade extras (rounded tube corners, flicker, static, rolling band). Every value is a global set each frame by
// MenuLook or LevelLook; zero means off, so nothing is stored in a material.
// Pass 1 (Interface) runs in Arcade Mode just before pass 0: it lays the interface, rendered into its own texture
// with premultiplied alpha, over the post-processed picture with a soft phosphor glow, so pass 0 bends, scans and
// fringes it with the game. It skips URP's bloom and grade, which washed out text and shifted colour cues.
Shader "Hidden/GeometricFlight/Screen Effects"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "ScreenEffects"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _ScreenGlitch;   // x tear amount, y seed, z blocks, w event scanlines
            float4 _ScreenEcho;     // x glitch echo (px), y glitch echo amount, z fringe (px at the edge), w fringe amount
            float4 _ScreenCrt;      // x curvature, y scanline strength, z scanline period (px), w grille
            float _ScreenCrtEdge;   // tube edge width (0..0.1 of the screen)
            float4 _ScreenArcade;   // x rounded corner radius, y flicker, z static noise, w rolling band
            half4 _ScreenAccent;
            TEXTURE2D(_ScreenInterface); // Arcade Mode's interface (alpha = where it's opaque); clear otherwise

            // Sine-free hash (Hoskins): the sine hash rounds some cells to exactly 0 on GPUs, which made them
            // pass "hash <= 0" and stay displaced with the glitch off.
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            half3 Sample(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
            half Peak(half3 c) { return max(c.r, max(c.g, c.b)); }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 texel = float2(abs(ddx(input.texcoord.x)), abs(ddy(input.texcoord.y)));
                float2 size = 1.0 / max(texel, 1e-6);

                // CRT curvature: a gentle bulge; outside the tube is black.
                float2 c = input.texcoord - 0.5;
                float2 uv = 0.5 + c * (1.0 + _ScreenCrt.x * dot(c, c) * 4.0);
                if (any(uv < 0.0) || any(uv > 1.0)) return half4(0, 0, 0, 1);
                float2 tube = uv;

                // Glitch: thin and chunky horizontal tears slide sideways; a sparse few blocks sample elsewhere.
                float seed = _ScreenGlitch.y;
                float band = floor(tube.y * 60.0), chunk = floor(tube.y * 14.0);
                // Strict "<" so an amount of zero can never select a band or block.
                float tear = (Hash(float2(band, seed)) < _ScreenGlitch.x * 0.22 || Hash(float2(chunk, seed + 3.1)) < _ScreenGlitch.x * 0.12) ? 1.0 : 0.0;
                uv.x += tear * (Hash(float2(band + chunk * 7.0, seed + 1.7)) - 0.5) * 0.08 * _ScreenGlitch.x;
                float2 cell = floor(tube * float2(32.0, 18.0));
                float block = Hash(cell + seed * 1.3) < _ScreenGlitch.z * 0.025 ? 1.0 : 0.0;
                uv += block * (float2(Hash(cell + 4.1), Hash(cell + 9.7)) - 0.5) * 0.05;

                half3 col = Sample(uv);

                // Glitch echo: an offset copy in the accent red, kept only where it adds light.
                if (_ScreenEcho.y > 0.0)
                    col = max(col, _ScreenAccent.rgb * Peak(Sample(uv - float2(_ScreenEcho.x * texel.x, 0.0))) * _ScreenEcho.y);

                // Palette fringing: a radial echo that grows toward the edges, like lens fringing, in the accent only.
                // Only where the offset copy is brighter than the pixel itself (outside edges), so the inside of a
                // coloured shape keeps its colour: blue guidance stays blue instead of turning magenta.
                if (_ScreenEcho.w > 0.0)
                {
                    float2 dir = c * 2.0;
                    half3 f = Sample(uv - dir * _ScreenEcho.z * texel);
                    // Much weaker on opaque interface, so dark text on a lit button stays readable.
                    half inside = SAMPLE_TEXTURE2D(_ScreenInterface, sampler_LinearClamp, uv).a;
                    col = max(col, _ScreenAccent.rgb * saturate(Peak(f) - Peak(col)) * _ScreenEcho.w * saturate(length(dir))
                        * (1.0h - 0.8h * inside));
                }

                float2 px = tube * size;
                // Event scanlines, strongest inside the tears.
                float eventScan = saturate(_ScreenGlitch.w * (0.35 + tear));
                col *= lerp(1.0, 0.5 + 0.5 * cos(px.y * TWO_PI / 3.0), eventScan) * (1.0 + eventScan * 0.45);
                col *= 1.0 + tear * 0.12;

                // CRT: scanlines and a monochrome aperture grille (no RGB mask, to stay in palette).
                col *= lerp(1.0, 0.5 + 0.5 * cos(px.y * TWO_PI / max(_ScreenCrt.z, 1.0)), _ScreenCrt.y) * (1.0 + _ScreenCrt.y * 0.45);
                col *= fmod(floor(px.x), 3.0) < 1.0 ? 1.0 : 1.0 - _ScreenCrt.w;
                if (_ScreenCrtEdge > 0.0)
                {
                    float2 e = smoothstep(0.0, _ScreenCrtEdge, tube) * smoothstep(0.0, _ScreenCrtEdge, 1.0 - tube);
                    col *= e.x * e.y;
                }

                // Arcade extras.
                float t = _Time.y;
                if (_ScreenArcade.x > 0.0)
                {
                    // Rounded tube corners: a rounded-rectangle mask, radius as a fraction of the half-screen.
                    float2 q = abs(tube - 0.5) * 2.0 - (1.0 - _ScreenArcade.x);
                    col *= 1.0 - smoothstep(0.85, 1.0, length(max(q, 0.0)) / _ScreenArcade.x);
                }
                if (_ScreenArcade.y > 0.0)
                    col *= 1.0 + _ScreenArcade.y * (sin(t * 47.0) * 0.6 + sin(t * 113.0) * 0.4);
                if (_ScreenArcade.w > 0.0)
                {
                    // A faint brighter band rolling slowly down the tube, as on an old monitor.
                    float d = abs(frac(tube.y + t * 0.08) - 0.5) * 2.0;
                    col *= 1.0 + _ScreenArcade.w * smoothstep(0.88, 1.0, d);
                }
                if (_ScreenArcade.z > 0.0)
                    col += (Hash(floor(px) + frac(t * 7.31) * 917.0) - 0.5) * _ScreenArcade.z;
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Interface"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_ScreenInterface);
            float4 _ScreenInterfaceLook;   // x brightness, y glow strength, z glow radius (px at 1080p)

            half3 Ui(float2 uv) { return SAMPLE_TEXTURE2D(_ScreenInterface, sampler_LinearClamp, uv).rgb; }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 ui = SAMPLE_TEXTURE2D(_ScreenInterface, sampler_LinearClamp, uv);
                half3 col = scene.rgb * (1.0h - ui.a) + ui.rgb * _ScreenInterfaceLook.x;

                // Phosphor glow: two rings of eight taps around the pixel, so lit strokes bleed softly outward. Only
                // where the interface is see-through, so text inside a filled button stays crisp.
                if (_ScreenInterfaceLook.y > 0.0)
                {
                    float2 texel = float2(abs(ddx(uv.x)), abs(ddy(uv.y)));
                    float r = _ScreenInterfaceLook.z * (1.0 / max(texel.y, 1e-6)) / 1080.0;
                    half3 glow = 0;
                    [unroll] for (int i = 0; i < 8; i++)
                    {
                        float a = i * (TWO_PI / 8.0);
                        float2 d = float2(cos(a), sin(a)) * texel * r;
                        glow += Ui(uv + d) + Ui(uv + d * 2.0);
                    }
                    col += glow / 16.0 * _ScreenInterfaceLook.y * _ScreenInterfaceLook.x * (1.0h - ui.a);
                }
                return half4(col, scene.a);
            }
            ENDHLSL
        }
    }
}

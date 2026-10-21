// Menu screen effects, run by MenuScreenFeature after URP post-processing, on the menu camera only.
// Glitch (tears, blocks, red echo, event scanlines), palette fringing (a radial echo in the accent red, so the
// palette stays black, white and red) and a CRT pass (curvature, scanlines, monochrome grille, tube edge).
// Every value is a global set each frame by MenuLook; zero means off, so nothing is stored in a material.
Shader "Hidden/GeometricFlight/Menu Screen"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "MenuScreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _MenuGlitch;   // x tear amount, y seed, z blocks, w event scanlines
            float4 _MenuEcho;     // x glitch echo (px), y glitch echo amount, z fringe (px at the edge), w fringe amount
            float4 _MenuCrt;      // x curvature, y scanline strength, z scanline period (px), w grille
            float _MenuCrtEdge;   // tube edge width (0..0.1 of the screen)
            half4 _MenuAccent;

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
                float2 uv = 0.5 + c * (1.0 + _MenuCrt.x * dot(c, c) * 4.0);
                if (any(uv < 0.0) || any(uv > 1.0)) return half4(0, 0, 0, 1);
                float2 tube = uv;

                // Glitch: thin and chunky horizontal tears slide sideways; a sparse few blocks sample elsewhere.
                float seed = _MenuGlitch.y;
                float band = floor(tube.y * 60.0), chunk = floor(tube.y * 14.0);
                // Strict "<" so an amount of zero can never select a band or block.
                float tear = (Hash(float2(band, seed)) < _MenuGlitch.x * 0.22 || Hash(float2(chunk, seed + 3.1)) < _MenuGlitch.x * 0.12) ? 1.0 : 0.0;
                uv.x += tear * (Hash(float2(band + chunk * 7.0, seed + 1.7)) - 0.5) * 0.08 * _MenuGlitch.x;
                float2 cell = floor(tube * float2(32.0, 18.0));
                float block = Hash(cell + seed * 1.3) < _MenuGlitch.z * 0.025 ? 1.0 : 0.0;
                uv += block * (float2(Hash(cell + 4.1), Hash(cell + 9.7)) - 0.5) * 0.05;

                half3 col = Sample(uv);

                // Glitch echo: an offset copy in the accent red, kept only where it adds light.
                if (_MenuEcho.y > 0.0)
                    col = max(col, _MenuAccent.rgb * Peak(Sample(uv - float2(_MenuEcho.x * texel.x, 0.0))) * _MenuEcho.y);

                // Palette fringing: a radial echo that grows toward the edges, like lens fringing, in the accent only.
                if (_MenuEcho.w > 0.0)
                {
                    float2 dir = c * 2.0;
                    half3 f = Sample(uv - dir * _MenuEcho.z * texel);
                    col = max(col, _MenuAccent.rgb * Peak(f) * _MenuEcho.w * saturate(length(dir)));
                }

                float2 px = tube * size;
                // Event scanlines, strongest inside the tears.
                float eventScan = saturate(_MenuGlitch.w * (0.35 + tear));
                col *= lerp(1.0, 0.5 + 0.5 * cos(px.y * TWO_PI / 3.0), eventScan) * (1.0 + eventScan * 0.45);
                col *= 1.0 + tear * 0.12;

                // CRT: scanlines and a monochrome aperture grille (no RGB mask, to stay in palette).
                col *= lerp(1.0, 0.5 + 0.5 * cos(px.y * TWO_PI / max(_MenuCrt.z, 1.0)), _MenuCrt.y) * (1.0 + _MenuCrt.y * 0.45);
                col *= fmod(floor(px.x), 3.0) < 1.0 ? 1.0 : 1.0 - _MenuCrt.w;
                if (_MenuCrtEdge > 0.0)
                {
                    float2 e = smoothstep(0.0, _MenuCrtEdge, tube) * smoothstep(0.0, _MenuCrtEdge, 1.0 - tube);
                    col *= e.x * e.y;
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

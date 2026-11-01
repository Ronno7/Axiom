// The Grid's sky: a camera-centred dome showing a dark cloud deck. Clouds are projected onto a plane
// overhead, so they converge toward the horizon. The upper field stays near the haze colour; cloud
// undersides pick up faint light near the horizon, as if lit by the ground far away. Drawn first, behind
// everything, without fog or depth.
Shader "GeometricFlight/Cloud Deck"
{
    Properties
    {
        _ZenithColor ("Upper sky", Color) = (0.0235294118, 0.0392156863, 0.0470588235, 1)
        [HDR] _CloudColor ("Lit cloud undersides", Color) = (0.0051815167, 0.0221738848, 0.0307134437, 1)
        _Amount ("Cloud cover", Range(0, 1)) = 0.5
        _Brightness ("Cloud brightness", Range(0, 4)) = 1
        _Scale ("Cloud size", Float) = 1
        _Drift ("Drift", Float) = 0.004
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _CloudColor;
                half _Amount, _Brightness;
                float _Scale, _Drift;
            CBUFFER_END

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // Broad masses first; finer octaves only break their edges.
            float Clouds(float2 p)
            {
                float v = 0.0, a = 0.55;
                for (int k = 0; k < 4; k++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.45; }
                return v;
            }

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.positionWS - _WorldSpaceCameraPos);
                float up = max(dir.y, 0.02);
                // Project onto a deck overhead: clouds shrink and crowd toward the horizon.
                float2 deck = dir.xz / up * (0.35 / max(_Scale, 0.01)) + _Time.y * _Drift;
                float n = Clouds(deck);
                float cover = smoothstep(1.0 - _Amount - 0.12, 1.0 - _Amount + 0.18, n);
                // The projection stretches into streaks at the horizon: dissolve the deck into the haze there.
                cover *= smoothstep(0.015, 0.09, dir.y);
                // Undersides are lit from far below, so light gathers near the horizon.
                float lowLight = pow(1.0 - saturate(dir.y), 5.0);
                float opening = pow(1.0 - saturate(dir.y), 22.0) * (1.0 - cover) * 0.35;
                half3 color = _ZenithColor.rgb
                    + _CloudColor.rgb * _Brightness * (cover * (0.18 + 0.82 * lowLight) + opening);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

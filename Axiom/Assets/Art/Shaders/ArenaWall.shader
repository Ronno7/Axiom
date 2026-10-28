// The arena's boundary as a sheet of light rather than more lines: faint everywhere, strongest near the floor, and
// brightening (with horizontal scan bands) within Near Range of the ship (_ArenaFocus, set by ArenaBounds), so the
// wall reads as a surface exactly when it matters. Additive, so it never hides what is behind it.
Shader "GeometricFlight/Arena Wall"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.44, 0.77, 0.88, 1)
        _BaseOpacity ("Base glow", Range(0, 0.2)) = 0.025
        _NearOpacity ("Glow near the ship", Range(0, 1)) = 0.3
        _NearRange ("Near range (m)", Float) = 160
        _TopFade ("Glow left at the ceiling", Range(0, 1)) = 0.25
        _BandSpacing ("Band spacing (m)", Float) = 8
        _BandWidth ("Band width (px)", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _BaseOpacity;
                half _NearOpacity;
                float _NearRange;
                half _TopFade;
                float _BandSpacing;
                float _BandWidth;
            CBUFFER_END
            float4 _ArenaFocus;

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = i.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float near = 1.0 - smoothstep(0.0, max(_NearRange, 1.0), distance(i.positionWS, _ArenaFocus.xyz));
                near *= near;
                half height = lerp(1.0h, _TopFade, (half)i.uv.y);
                float c = i.positionWS.y / max(_BandSpacing, 0.01);
                float d = abs(frac(c - 0.5) - 0.5) / max(fwidth(c), 1e-5);
                float band = 1.0 - saturate(d - _BandWidth * 0.5 + 0.5);
                half glow = (_BaseOpacity + near * _NearOpacity) * height + band * near * 0.6;
                return half4(_Color.rgb * glow, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

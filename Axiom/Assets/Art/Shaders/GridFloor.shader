// The arena floor: dark glass with self-lit grid lines in world space (minor and major spacing). Line width is
// in screen pixels (fwidth), so lines stay crisp near and far without shimmering, and they fade out with
// distance from the camera so the far grid doesn't turn to moiré. Line colour is HDR, so lines feed bloom.
Shader "GeometricFlight/Grid Floor"
{
    Properties
    {
        _BaseColor ("Floor", Color) = (0.02, 0.035, 0.05, 1)
        [HDR] _LineColor ("Lines", Color) = (0.6, 1.6, 2.0, 1)
        _MinorSpacing ("Minor spacing (m)", Float) = 10
        _MajorSpacing ("Major spacing (m)", Float) = 50
        _MinorWidth ("Minor width (px)", Float) = 1.2
        _MajorWidth ("Major width (px)", Float) = 2.2
        _MinorStrength ("Minor strength", Range(0, 1)) = 0.35
        _Fade ("Fade start, end (m)", Vector) = (60, 320, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "GridForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                float _MinorSpacing;
                float _MajorSpacing;
                float _MinorWidth;
                float _MajorWidth;
                half _MinorStrength;
                float4 _Fade;
            CBUFFER_END

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // 1 on a line, falling to 0 over about a pixel at its edges.
            float Grid(float2 p, float spacing, float widthPx)
            {
                float2 c = p / max(spacing, 0.001);
                float2 d = abs(frac(c - 0.5) - 0.5) / max(fwidth(c), 1e-5);
                return 1.0 - saturate(min(d.x, d.y) - widthPx * 0.5 + 0.5);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float major = Grid(p, _MajorSpacing, _MajorWidth);
                float minor = Grid(p, _MinorSpacing, _MinorWidth) * _MinorStrength;
                float distance = length(i.positionWS - _WorldSpaceCameraPos);
                float fade = 1.0 - smoothstep(_Fade.x, _Fade.y, distance);
                // Minor lines go first: they alias soonest.
                float minorFade = 1.0 - smoothstep(_Fade.x * 0.5, _Fade.y * 0.6, distance);
                float lines = max(major * fade, minor * minorFade);
                return half4(_BaseColor.rgb + _LineColor.rgb * lines, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

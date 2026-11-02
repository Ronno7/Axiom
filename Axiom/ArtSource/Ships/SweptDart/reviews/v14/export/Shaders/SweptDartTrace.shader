// One mesh/material; each spline reads its own row of the sampled v13 animation.
Shader "GeometricFlight/Swept Dart Trace"
{
    Properties
    {
        _BaseColor("Palette Cyan", Color) = (0.435294118, 0.764705882, 0.874509804, 1)
        _Timeline("Trace Timeline (Linear, Point)", 2D) = "black" {}
        _BuildProgress("Build Progress", Range(0, 1)) = 1
        _EmissionScale("Emission Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "SweptDartTraceForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Timeline);
            SAMPLER(sampler_Timeline);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _BuildProgress;
                float _EmissionScale;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 reveal : TEXCOORD2;
                float2 trace : TEXCOORD3;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 reveal : TEXCOORD0;
                float2 trace : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.reveal = i.reveal;
                o.trace = i.trace;
                o.fogCoord = o.positionCS.z;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float samplePosition = saturate(_BuildProgress) * 288.0;
                float lower = floor(samplePosition);
                float4 a = SAMPLE_TEXTURE2D_LOD(_Timeline, sampler_Timeline, float2((lower + 0.5) / 289.0, i.trace.x), 0);
                float4 b = SAMPLE_TEXTURE2D_LOD(_Timeline, sampler_Timeline, float2((min(lower + 1.0, 288.0) + 0.5) / 289.0, i.trace.x), 0);
                float2 drawAndLight = lerp(a.rg, b.rg, frac(samplePosition));
                clip(a.b - 0.5);
                clip(drawAndLight.x - i.reveal.y - 0.000001);
                half3 color = _BaseColor.rgb * max(0, drawAndLight.y) * _EmissionScale;
                return half4(MixFogColor(color, half3(0, 0, 0), ComputeFogFactor(i.fogCoord)), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// Staged source only. Compile and visually verify in Unity during the future import.
Shader "GeometricFlight/Swept Dart Surface"
{
    Properties
    {
        _BaseColor("Palette Base", Color) = (0.901960784, 1, 1, 1)
        _EdgeColor("Palette Construction Cyan", Color) = (0.435294118, 0.764705882, 0.874509804, 1)
        _EmissionStrength("Permanent Emission", Float) = 0
        _ShadeStrength("Facet Shading", Range(0, 1)) = 0.65
        _BuildProgress("Build Progress", Range(0, 1)) = 1
        _ArrivalMap("Arrival Data (Linear)", 2D) = "white" {}
        [Toggle] _UseArrivalMap("Hull Arrival Map", Float) = 0
        _RetireAt("Retire At Reference Seconds", Float) = 4
        _FrontDuration("Front Duration", Float) = 0.055
        _FrontStrength("Front Emission", Float) = 12
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        Pass
        {
            Name "SweptDartForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ArrivalMap);
            SAMPLER(sampler_ArrivalMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _EmissionStrength;
                float _ShadeStrength;
                float _BuildProgress;
                float _UseArrivalMap;
                float _RetireAt;
                float _FrontDuration;
                float _FrontStrength;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 reveal : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 reveal : TEXCOORD2;
                float fogCoord : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.reveal = input.reveal;
                o.fogCoord = o.positionCS.z;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float seconds = saturate(_BuildProgress) * 3.0;
                float arrival = i.reveal.x;
                if (_UseArrivalMap > 0.5)
                    arrival = SAMPLE_TEXTURE2D(_ArrivalMap, sampler_ArrivalMap, i.uv).r;
                float delta = seconds - arrival * 3.0;
                clip(seconds - 0.000001);
                clip(delta - 0.000001);
                clip(_RetireAt - seconds - 0.000001);
                half light = saturate(dot(normalize(i.normalWS), normalize(float3(-0.4, 0.8, -0.6))));
                half shade = lerp(1.0h, 0.3h + 0.7h * light, _ShadeStrength);
                half3 color = _BaseColor.rgb * (shade + _EmissionStrength);
                if (delta < _FrontDuration)
                    color = _EdgeColor.rgb * _FrontStrength;
                return half4(MixFog(color, ComputeFogFactor(i.fogCoord)), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

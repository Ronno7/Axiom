Shader "GeometricFlight/Foundation Flat"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadeStrength("Facet Shading", Range(0, 1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "FoundationForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _ShadeStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Deliberately simple art-direction lighting for the blockout.
                // This shader does not receive realtime lights or shadows.
                half light = saturate(dot(normalize(input.normalWS), normalize(float3(-0.4, 0.8, -0.6))));
                half shade = lerp(1.0h, 0.3h + 0.7h * light, _ShadeStrength);
                return half4(_BaseColor.rgb * shade, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

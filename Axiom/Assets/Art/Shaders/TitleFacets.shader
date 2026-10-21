// Flat-faceted shading for the 3D menu title: a key light, a fill toward the camera that keeps the face
// bright, and a shadow floor for faces that see neither. With the key from the upper right and the title camera
// on the upper left, the visible left sides of the extrusion fall into shadow and the top bevels catch light.
Shader "GeometricFlight/Title Facets"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _KeyDirection ("Key light direction (toward the light)", Vector) = (0.45, 0.65, -0.6, 0)
        _Key ("Key light", Range(0, 1.5)) = 0.9
        _Fill ("Front fill (toward the camera)", Range(0, 1)) = 0.35
        _FillFocus ("Fill focus (higher = face only, bevels darker)", Range(1, 8)) = 4
        _Shadow ("Shadow floor", Range(0, 1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "TitleForward"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _KeyDirection;
                half _Key;
                half _Fill;
                half _FillFocus;
                half _Shadow;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 toCamera = normalize(GetWorldSpaceViewDir(input.positionWS));
                half key = saturate(dot(n, normalize(_KeyDirection.xyz))) * _Key;
                half fill = pow(saturate(dot(n, toCamera)), _FillFocus) * _Fill;
                half shade = saturate(_Shadow + key + fill);
                return half4(_BaseColor.rgb * shade, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

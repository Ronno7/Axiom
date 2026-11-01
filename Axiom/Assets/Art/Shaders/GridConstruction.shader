Shader "GeometricFlight/Grid Construction"
{
    Properties
    {
        _BaseColor("Hull", Color) = (0.22745098, 0.325490196, 0.384313725, 1)
        [HDR] _EdgeColor("Construction front", Color) = (0.397402088, 1.36431115, 1.84477602, 1)
        _BuildFront("World Z front", Float) = -10000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float fog : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EdgeColor;
                float _BuildFront;
            CBUFFER_END
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float distance = i.positionWS.z - _BuildFront;
                clip(distance);
                half light = 0.45h + 0.55h * saturate(dot(normalize(i.normalWS), normalize(float3(-0.4,0.8,-0.6))));
                half edge = 1 - smoothstep(0,0.18,distance);
                return half4(MixFog(_BaseColor.rgb * light + _EdgeColor.rgb * edge, i.fog),1);
            }
            ENDHLSL
        }
    }
}

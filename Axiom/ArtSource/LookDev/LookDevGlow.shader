// Lookdev scene material, additive, unaffected by fog.
//   Mode 0: horizon band. Brightest along the quad's middle, fading up/down and toward the ends.
//   Mode 1: soft dot (stars, dust). Vertex colour alpha scales brightness.
Shader "AxiomLook/AdditiveGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Power ("Falloff", Float) = 2
        _Mode ("Mode (0 band, 1 dot)", Float) = 0
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
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Power;
                half _Mode;
            CBUFFER_END
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half g;
                if (_Mode < 0.5)
                {
                    half v = 1.0h - abs(i.uv.y * 2.0h - 1.0h);
                    half u = sin(i.uv.x * 3.14159h);
                    g = pow(saturate(v), _Power) * u * u;
                }
                else
                {
                    half d = length(i.uv - 0.5h) * 2.0h;
                    g = pow(saturate(1.0h - d), _Power);
                }
                return half4(_Color.rgb * g * i.color.a, 1.0h);
            }
            ENDHLSL
        }
    }
}

Shader "GeometricFlight/Passage Construction"
{
    // Enclosed passage geometry that builds itself as _BuildFront (world Z) advances.
    // Surfaces (_Path 0) appear behind the front with a glowing seam at it.
    // Lines (_Path 1) draw along their own path: uv.x is 0..1 along the line and uv.y is the
    // world Z where the line starts drawing, so a rib's legs rise and meet over _DrawLength metres
    // of front travel. Longitudinal lines use uv.x 0 and race ahead with the front.
    Properties
    {
        [HDR] _BaseColor("Built", Color) = (0.00367650732, 0.00699541019, 0.013702083, 1)
        [HDR] _EdgeColor("Construction front", Color) = (0.397402088, 1.36431115, 1.84477602, 1)
        _BuildFront("World Z front", Float) = -10000
        _DrawLength("Draw length (m)", Float) = 18
        _Path("Reveal along path (0 surface, 1 line)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float fog : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EdgeColor;
                float _BuildFront, _DrawLength, _Path;
            CBUFFER_END
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half edge;
                if (_Path > 0.5)
                {
                    float progress = saturate((_BuildFront - i.uv.y) / max(_DrawLength, 0.001));
                    clip(progress - i.uv.x - 0.0001);
                    // The drawing tip glows; a finished line is quiet.
                    edge = (1 - smoothstep(0, 0.08, progress - i.uv.x)) * step(progress, 0.999);
                }
                else
                {
                    float built = _BuildFront - i.positionWS.z;
                    clip(built);
                    edge = 1 - smoothstep(0, 2.5, built);
                }
                return half4(MixFog(_BaseColor.rgb + _EdgeColor.rgb * edge, i.fog), 1);
            }
            ENDHLSL
        }
    }
}

// Lookdev only: lays the separately rendered UI (premultiplied alpha) over the processed world image.
Shader "Hidden/AxiomLook/AlphaOver"
{
    Properties { _MainTex ("UI", 2D) = "black" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i) : SV_Target { return tex2D(_MainTex, i.uv); }
            ENDCG
        }
    }
}

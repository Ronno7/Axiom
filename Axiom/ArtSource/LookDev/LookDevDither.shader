// Lookdev: 1-bit ordered dither (8x8 Bayer) on the world image. Luminance becomes black or white dots;
// strongly red pixels dither to the accent red instead, so the single accent colour survives.
Shader "Hidden/AxiomLook/Dither1Bit"
{
    Properties
    {
        _MainTex ("World", 2D) = "black" {}
        _Pixel ("Dot size (px)", Float) = 2
        _Gamma ("Tone gamma", Float) = 0.8
        _Light ("Light", Color) = (0.92, 0.92, 0.92, 1)
        _Dark ("Dark", Color) = (0, 0, 0, 1)
        _Accent ("Accent", Color) = (0.85, 0.12, 0.16, 1)
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Pixel, _Gamma;
            fixed4 _Light, _Dark, _Accent;

            static const float BAYER[64] = {
                 0, 32,  8, 40,  2, 34, 10, 42,  48, 16, 56, 24, 50, 18, 58, 26,
                12, 44,  4, 36, 14, 46,  6, 38,  60, 28, 52, 20, 62, 30, 54, 22,
                 3, 35, 11, 43,  1, 33,  9, 41,  51, 19, 59, 27, 49, 17, 57, 25,
                15, 47,  7, 39, 13, 45,  5, 37,  63, 31, 55, 23, 61, 29, 53, 21 };

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 cell = floor(i.uv * _MainTex_TexelSize.zw / _Pixel);
                float2 uv = (cell + 0.5) * _Pixel * _MainTex_TexelSize.xy;     // sample once per dot
                float3 col = tex2D(_MainTex, uv).rgb;
                float lum = pow(saturate(dot(col, float3(0.299, 0.587, 0.114))), _Gamma);
                int idx = (int)fmod(cell.y, 8.0) * 8 + (int)fmod(cell.x, 8.0);
                float on = step((BAYER[idx] + 0.5) / 64.0, lum);
                float red = saturate((col.r - max(col.g, col.b)) * 6.0);
                float3 ink = lerp(_Light.rgb, _Accent.rgb, step(0.35, red));
                float onRed = step((BAYER[idx] + 0.5) / 64.0, saturate(col.r * 2.2));
                on = red > 0.35 ? onRed : on;
                return fixed4(lerp(_Dark.rgb, ink, on), 1);
            }
            ENDCG
        }
    }
}

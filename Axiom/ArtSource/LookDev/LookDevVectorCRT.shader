// Lookdev: vector-monitor finish. Gentle tube curvature, horizontal scanlines, a faint aperture-grille mask
// and tube-edge falloff. Runs on the world image only (after bloom), never on the UI.
Shader "Hidden/AxiomLook/VectorCRT"
{
    Properties
    {
        _MainTex ("World", 2D) = "black" {}
        _Curve ("Curvature", Range(0, 0.3)) = 0.06
        _Scan ("Scanline strength", Range(0, 1)) = 0.32
        _ScanPeriod ("Scanline period (px)", Float) = 3
        _Mask ("Aperture mask", Range(0, 0.5)) = 0.08
        _Edge ("Tube edge width", Range(0, 0.1)) = 0.035
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
            float _Curve, _Scan, _ScanPeriod, _Mask, _Edge;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float2 uv = 0.5 + c * (1.0 + _Curve * dot(c, c) * 4.0);
                if (any(uv < 0.0) || any(uv > 1.0)) return fixed4(0, 0, 0, 1);
                float3 col = tex2D(_MainTex, uv).rgb;

                float2 px = uv * _MainTex_TexelSize.zw;
                float scan = 0.5 + 0.5 * cos(px.y * 6.2831853 / _ScanPeriod);
                col *= lerp(1.0, scan, _Scan) * (1.0 + _Scan * 0.45);         // keep overall brightness

                float m = fmod(floor(px.x), 3.0);
                float3 grille = m < 1.0 ? float3(1, 1 - _Mask, 1 - _Mask)
                              : m < 2.0 ? float3(1 - _Mask, 1, 1 - _Mask) : float3(1 - _Mask, 1 - _Mask, 1);
                col *= grille;

                if (_Edge > 0.0)
                {
                    float2 e = smoothstep(0.0, _Edge, uv) * smoothstep(0.0, _Edge, 1.0 - uv);
                    col *= e.x * e.y;                                          // soft tube edge
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}

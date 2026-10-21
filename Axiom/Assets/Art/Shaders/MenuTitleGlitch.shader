// Glitch for the 3D title's render texture (transparent), applied by MenuLook with Graphics.Blit during a glitch
// event: horizontal tears, an echo in the accent red, and scanlines. Alpha-aware.
Shader "Hidden/GeometricFlight/Menu Title Glitch"
{
    Properties
    {
        _MainTex ("Title", 2D) = "black" {}
        _Tear ("Tear amount", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 1
        _Echo ("Echo offset (px)", Float) = 0
        _EchoAmount ("Echo strength", Range(0, 1)) = 0.9
        _Scan ("Scanlines", Range(0, 1)) = 0
        _Accent ("Accent", Color) = (0.84, 0.12, 0.16, 1)
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
            float _Tear, _Seed, _Echo, _EchoAmount, _Scan;
            fixed4 _Accent;

            float hash(float2 p)   // sine-free (Hoskins), see Menu Screen
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float band = floor(uv.y * 60.0), chunk = floor(uv.y * 14.0);
                float tear = (hash(float2(band, _Seed)) < _Tear * 0.22 || hash(float2(chunk, _Seed + 3.1)) < _Tear * 0.12) ? 1.0 : 0.0;
                uv.x += tear * (hash(float2(band + chunk * 7.0, _Seed + 1.7)) - 0.5) * 0.08 * _Tear;

                fixed4 c = tex2D(_MainTex, uv);
                fixed4 g = tex2D(_MainTex, uv - float2(_Echo * _MainTex_TexelSize.x, 0));
                float e = max(g.r, max(g.g, g.b)) * _EchoAmount * step(0.001, _Echo);
                c.rgb = max(c.rgb, _Accent.rgb * e);
                c.a = max(c.a, g.a * _EchoAmount * 0.9 * step(0.001, _Echo));

                float scan = 0.5 + 0.5 * cos(i.uv.y * _MainTex_TexelSize.w * 6.2831853 / 3.0);
                float s = saturate(_Scan * (0.35 + tear));
                c.rgb *= lerp(1.0, scan, s) * (1.0 + s * 0.45);
                return c;
            }
            ENDCG
        }
    }
}

// Lookdev: signal glitch, in the game's palette. Thin horizontal tears slide sideways, a few blocks displace,
// an echo offsets in the accent red (not rainbow RGB), tears get scanlines and a touch of light, and a faint
// roll bar can travel down the frame. Alpha-aware, so it also runs on the title's transparent render texture.
Shader "Hidden/AxiomLook/Glitch"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _Slice ("Tear amount", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 1
        _Ghost ("Echo offset (px)", Float) = 0
        _GhostAmt ("Echo strength", Range(0, 1)) = 0
        _Block ("Block displacement", Range(0, 1)) = 0
        _Scan ("Scanlines", Range(0, 1)) = 0
        _RollY ("Roll bar position (uv y, <0 off)", Float) = -1
        _RollAmt ("Roll bar strength", Range(0, 1)) = 1
        _Echo ("Echo colour", Color) = (0.84, 0.12, 0.16, 1)
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
            float _Slice, _Seed, _Ghost, _GhostAmt, _Block, _Scan, _RollY, _RollAmt;
            fixed4 _Echo;

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                // Tears: thin bands plus a few chunkier ones, each sliding by its own amount.
                float band = floor(uv.y * 60.0);
                float chunk = floor(uv.y * 14.0);
                float tear = max(step(hash(float2(band, _Seed)), _Slice * 0.22),
                                 step(hash(float2(chunk, _Seed + 3.1)), _Slice * 0.12));
                uv.x += tear * (hash(float2(band + chunk * 7.0, _Seed + 1.7)) - 0.5) * 0.08 * _Slice;
                // Blocks: a sparse few cells sample from elsewhere.
                float2 cell = floor(i.uv * float2(32.0, 18.0));
                float blk = step(hash(cell + _Seed * 1.3), _Block * 0.025);
                uv += blk * (float2(hash(cell + 4.1), hash(cell + 9.7)) - 0.5) * 0.05;
                // Roll bar: a soft band with a hair of horizontal shift.
                float roll = _RollY >= 0.0 ? saturate(1.0 - abs(i.uv.y - _RollY) / 0.03) * _RollAmt : 0.0;
                uv.x += roll * 0.004;

                fixed4 c = tex2D(_MainTex, uv);
                // Red echo: an offset copy, kept only where it adds light.
                fixed4 g = tex2D(_MainTex, uv - float2(_Ghost * _MainTex_TexelSize.x, 0));
                float gl = max(g.r, max(g.g, g.b)) * _GhostAmt;
                c.rgb = max(c.rgb, _Echo.rgb * gl);
                c.a = max(c.a, g.a * _GhostAmt * 0.9);

                float scan = 0.5 + 0.5 * cos(i.uv.y * _MainTex_TexelSize.w * 6.2831853 / 3.0);
                float scanAmt = saturate(_Scan * (0.35 + tear));
                c.rgb *= lerp(1.0, scan, scanAmt) * (1.0 + scanAmt * 0.45);
                c.rgb *= 1.0 + tear * 0.12;
                c.rgb += roll * 0.05;
                return c;
            }
            ENDCG
        }
    }
}

Shader "Hidden/Mizotake/LED Gallery/Surface Maps"
{
    Properties { _SurfaceKind("Surface Kind", Float) = 0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "UnityCG.cginc"
        float _SurfaceKind;
        float Hash(float2 p) { p = frac(p * float2(0.1031, 0.11369)); p += dot(p, p.yx + 19.19); return frac((p.x + p.y) * p.x); }
        float Noise(float2 uv, float cells)
        {
            float2 p = uv * cells;
            float2 f = frac(p); f = f * f * (3 - 2 * f);
            float2 i = floor(p);
            return lerp(lerp(Hash(fmod(i, cells)), Hash(fmod(i + float2(1, 0), cells)), f.x), lerp(Hash(fmod(i + float2(0, 1), cells)), Hash(fmod(i + 1, cells)), f.x), f.y);
        }
        float Height(float2 uv)
        {
            if (_SurfaceKind > 1.5) return sin(uv.x * 6.283185 * 256) * 0.012 + Noise(uv, 128) * 0.03;
            if (_SurfaceKind > 0.5) return sin(uv.x * 6.283185 * 128) * sin(uv.y * 6.283185 * 128) * 0.045 + Noise(uv, 256) * 0.02;
            float2 edge = min(frac(uv), 1 - frac(uv));
            float seam = 1 - smoothstep(0.001, 0.006, min(edge.x, edge.y));
            return Noise(uv, 64) * 0.025 + Noise(uv, 256) * 0.015 - seam * 0.035;
        }
        ENDHLSL
        Pass
        {
            Name "Albedo"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Albedo
            half4 Albedo(v2f_img input) : SV_Target
            {
                float grain = lerp(0.82, 1, Noise(input.uv, 256));
                if (_SurfaceKind < 0.5)
                {
                    float2 edge = min(input.uv, 1 - input.uv);
                    grain *= lerp(0.55, 1, smoothstep(0.001, 0.006, min(edge.x, edge.y)));
                }
                else if (_SurfaceKind < 1.5) grain *= 0.92 + 0.08 * sin(input.uv.x * 6.283185 * 128) * sin(input.uv.y * 6.283185 * 128);
                return half4(grain, grain, grain, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Normal"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Normal
            half4 Normal(v2f_img input) : SV_Target
            {
                float2 d = float2(1.0 / 1024, 0);
                float2 slope = float2(Height(input.uv + d) - Height(input.uv - d), Height(input.uv + d.yx) - Height(input.uv - d.yx));
                float3 n = normalize(float3(-slope * 3, 1));
                return half4(n * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Metallic Smoothness"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Mask
            half4 Mask(v2f_img input) : SV_Target
            {
                float smoothness = _SurfaceKind > 1.5 ? lerp(0.4, 0.65, Noise(input.uv, 128)) : (_SurfaceKind > 0.5 ? lerp(0.08, 0.16, Noise(input.uv, 64)) : lerp(0.16, 0.32, Noise(input.uv, 64)));
                return half4(_SurfaceKind > 1.5 ? 0.72 : 0, 0, 0, smoothness);
            }
            ENDHLSL
        }
    }
}

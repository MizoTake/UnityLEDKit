#ifndef MIZOTAKE_LED_DEPTH_INCLUDED
#define MIZOTAKE_LED_DEPTH_INCLUDED
#include "LedCommon.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
half4 LedDepthFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return input.positionCS.z;
}
half4 LedNormalFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    UNITY_SETUP_INSTANCE_ID(input);
    float2 grid = max(_LedResolution.xy, 1);
    float2 coordinate = input.uv * grid;
    float2 cell = frac(coordinate);
    float2 footprint = max(abs(ddx(coordinate)) + abs(ddy(coordinate)), 0.0001);
    float2 lens;
    float mask;
    #if defined(LED_DIFFUSED)
    float radius = _Fill * 0.5;
    float aa = max(max(footprint.x, footprint.y) * 0.5, 0.005);
    mask = (1 - smoothstep(radius - aa, radius + aa, length(cell - 0.5))) * (1 - smoothstep(0.35, 1, max(footprint.x, footprint.y)));
    lens = (cell - 0.5) / radius;
    #else
    float width = _Fill / 6;
    float height = _Fill * 0.5;
    float mono = LedRoundedMask(cell - 0.5, float2(height, height), footprint);
    float rgb = LedRoundedMask(float2((frac(cell.x * 3) - 0.5) / 3, cell.y - 0.5), float2(width, height), footprint);
    mask = lerp(mono, rgb, _RgbSeparation) * (1 - smoothstep(0.25, 1, max(footprint.x * 3, footprint.y)));
    lens = float2(lerp(cell.x - 0.5, frac(cell.x * 3) - 0.5, _RgbSeparation), cell.y - 0.5) * 2 / _Fill;
    #endif
    float3 normal = LedSurfaceNormal(input, mask, lens);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
    #else
    return half4(normal, 0);
    #endif
}
#endif

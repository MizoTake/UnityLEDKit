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
    float3 normal = normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
    #else
    return half4(normal, 0);
    #endif
}
#endif

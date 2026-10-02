#ifndef MIZOTAKE_LED_COMMON_INCLUDED
#define MIZOTAKE_LED_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BlendMap);
SAMPLER(sampler_BlendMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _LedResolution;
float4 _ContentRect;
half4 _Tint;
half4 _HousingColor;
float _Brightness;
float _Transition;
float _RgbSeparation;
float _Fill;
float _Diffusion;
float _ViewingAngle;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half fog : TEXCOORD3;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings LedVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fog = ComputeFogFactor(position.positionCS.z);
    return output;
}

half3 LedContent(float2 uv, out float2 cell, out float2 footprint)
{
    float2 grid = max(_LedResolution.xy, 1.0);
    float2 coordinate = uv * grid;
    footprint = max(abs(ddx(coordinate)) + abs(ddy(coordinate)), 0.0001);
    cell = frac(coordinate);
    float2 centerUV = (floor(coordinate) + 0.5) / grid;
    // Keep texture derivatives continuous across cell boundaries to avoid unstable mip selection.
    float2 videoUV = centerUV * _ContentRect.xy + _ContentRect.zw;
    half3 first = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, videoUV, ddx(uv) * _ContentRect.xy, ddy(uv) * _ContentRect.xy).rgb;
    half3 second = SAMPLE_TEXTURE2D_GRAD(_BlendMap, sampler_BlendMap, videoUV, ddx(uv) * _ContentRect.xy, ddy(uv) * _ContentRect.xy).rgb;
    return lerp(first, second, saturate(_Transition)) * _Tint.rgb;
}

float LedRoundedMask(float2 localPosition, float2 halfSize, float2 footprint)
{
    float2 edge = abs(localPosition) - halfSize + 0.06;
    float distance = length(max(edge, 0.0)) + min(max(edge.x, edge.y), 0.0) - 0.06;
    float aa = max(max(footprint.x, footprint.y) * 0.5, 0.001);
    return 1.0 - smoothstep(-aa, aa, distance);
}

half3 LedFinish(Varyings input, half3 emission, half mask)
{
    half angle = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
    half falloff = lerp(1.0h, pow(max(angle, 0.001h), 0.6h), _ViewingAngle);
    return MixFog(_HousingColor.rgb * (1.0h - mask * 0.7h) + emission * _Brightness * falloff, input.fog);
}
#endif

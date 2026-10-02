#ifndef MIZOTAKE_LED_COMMON_INCLUDED
#define MIZOTAKE_LED_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
float _SurfaceLighting;
float _LensCurvature;
float _LensSmoothness;
float _HousingSmoothness;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
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
    half4 tangentWS : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings LedVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
    output.uv = input.uv;
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
    float2 videoUV = (centerUV * _BaseMap_ST.xy + _BaseMap_ST.zw) * _ContentRect.xy + _ContentRect.zw;
    float2 contentScale = _BaseMap_ST.xy * _ContentRect.xy;
    half3 first = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, videoUV, ddx(uv) * contentScale, ddy(uv) * contentScale).rgb;
    half3 second = SAMPLE_TEXTURE2D_GRAD(_BlendMap, sampler_BlendMap, videoUV, ddx(uv) * contentScale, ddy(uv) * contentScale).rgb;
    return max(lerp(first, second, saturate(_Transition)) * _Tint.rgb, 0);
}

float LedRoundedMask(float2 localPosition, float2 halfSize, float2 footprint)
{
    float corner = min(0.06, min(halfSize.x, halfSize.y));
    float2 edge = abs(localPosition) - halfSize + corner;
    float distance = length(max(edge, 0.0)) + min(max(edge.x, edge.y), 0.0) - corner;
    float aa = max(max(footprint.x, footprint.y) * 0.5, 0.001);
    return 1.0 - smoothstep(-aa, aa, distance);
}

float LedRoundedArea(float width, float height)
{
    float corner = min(0.06, min(width, height) * 0.5);
    return max(width * height - (4.0 - PI) * corner * corner, 0.001);
}

half3 LedSurfaceNormal(Varyings input, half mask, float2 lensPosition)
{
    half3 baseNormal = normalize(input.normalWS);
    half3 tangent = input.tangentWS.xyz;
    if (dot(tangent, tangent) < 0.0001h) tangent = cross(baseNormal, abs(baseNormal.y) < 0.95h ? half3(0, 1, 0) : half3(1, 0, 0));
    tangent = normalize(tangent);
    half3 bitangent = cross(baseNormal, tangent) * (abs(input.tangentWS.w) < 0.5h ? 1.0h : input.tangentWS.w);
    float2 slope = lensPosition * (_LensCurvature * mask * _SurfaceLighting);
    return normalize(baseNormal + tangent * slope.x + bitangent * slope.y);
}

half3 LedFinish(Varyings input, half3 emission, half mask, float2 lensPosition)
{
    half3 baseNormal = normalize(input.normalWS);
    half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half angle = saturate(dot(baseNormal, viewDirection));
    half falloff = lerp(1.0h, pow(max(angle, 0.001h), 0.6h), _ViewingAngle);
    half3 radiance = emission * _Brightness * falloff;
    half3 unlit = _HousingColor.rgb * (1.0h - mask * 0.7h) + radiance;
    // Fade surface relief with the resolved emitter mask so small LEDs do not sparkle at a distance.
    half3 lensNormal = LedSurfaceNormal(input, mask, lensPosition);
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = _HousingColor.rgb * lerp(1.0h, 0.25h, mask);
    surface.metallic = 0;
    surface.smoothness = lerp(_HousingSmoothness, _LensSmoothness, mask);
    surface.occlusion = 1;
    surface.alpha = 1;
    surface.emission = radiance;
    InputData lighting = (InputData)0;
    lighting.positionWS = input.positionWS;
    lighting.normalWS = lensNormal;
    lighting.viewDirectionWS = viewDirection;
    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    lighting.bakedGI = SampleSH(lensNormal);
    lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    lighting.shadowMask = half4(1, 1, 1, 1);
    half3 physical = UniversalFragmentPBR(lighting, surface).rgb;
    return MixFog(lerp(unlit, physical, _SurfaceLighting), input.fog);
}
#endif

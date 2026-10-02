Shader "Mizotake/LED Wall/RGB LED"
{
    Properties
    {
        [MainTexture] _BaseMap("Video / Texture", 2D) = "black" {}
        [NoScaleOffset] _BlendMap("Second Video / Texture", 2D) = "black" {}
        _Transition("Crossfade A to B", Range(0, 1)) = 0
        [HDR] _Tint("Emission Tint", Color) = (1, 1, 1, 1)
        _HousingColor("Black LED Housing", Color) = (0.006, 0.008, 0.012, 1)
        _LedResolution("LED Columns / Rows", Vector) = (256, 144, 0, 0)
        _ContentRect("Content UV Scale / Offset", Vector) = (1, 1, 0, 0)
        _Brightness("HDR Brightness", Range(0, 20)) = 2.5
        _RgbSeparation("RGB Subpixels", Range(0, 1)) = 1
        _Fill("LED Fill", Range(0.2, 0.95)) = 0.72
        _Diffusion("Optical Diffusion", Range(0, 1)) = 0.15
        _ViewingAngle("Off Axis Dimming", Range(0, 1)) = 0.4
        _SurfaceLighting("Physical Surface Lighting", Range(0, 1)) = 1
        _LensCurvature("Lens Curvature", Range(0, 1)) = 0.35
        _LensSmoothness("Lens Smoothness", Range(0, 1)) = 0.82
        _HousingSmoothness("Housing Smoothness", Range(0, 1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "RGB LED"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LedVertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "LedCommon.hlsl"
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                UNITY_SETUP_INSTANCE_ID(input);
                float2 cell, footprint;
                half3 video = LedContent(input.uv, cell, footprint);
                float width = _Fill / 6.0;
                float height = _Fill * 0.5;
                half3 masks = half3(LedRoundedMask(cell - float2(1.0/6.0, 0.5), float2(width, height), footprint), LedRoundedMask(cell - float2(0.5, 0.5), float2(width, height), footprint), LedRoundedMask(cell - float2(5.0/6.0, 0.5), float2(width, height), footprint));
                half monoMask = LedRoundedMask(cell - 0.5, float2(_Fill * 0.5, height), footprint);
                float resolved = 1.0 - smoothstep(0.25, 1.0, max(footprint.x * 3.0, footprint.y));
                // Normalize covered area so far-field radiance matches the resolved subpixel average.
                half3 rgb = video * masks / LedRoundedArea(_Fill / 3.0, _Fill);
                half3 mono = video * monoMask / LedRoundedArea(_Fill, _Fill);
                half3 emission = lerp(video, lerp(mono, rgb, _RgbSeparation), resolved * (1.0 - _Diffusion * 0.65));
                half surfaceMask = lerp(monoMask, saturate(masks.r + masks.g + masks.b), _RgbSeparation) * resolved;
                float2 lens = float2(lerp(cell.x - 0.5, frac(cell.x * 3.0) - 0.5, _RgbSeparation), cell.y - 0.5) * 2.0 / _Fill;
                return half4(LedFinish(input, emission, surfaceMask, lens), 1.0h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            ZWrite On
            HLSLPROGRAM
            #pragma vertex LedVertex
            #pragma fragment LedDepthFragment
            #pragma multi_compile_instancing
            #include "LedDepth.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex LedVertex
            #pragma fragment LedNormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "LedDepth.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}

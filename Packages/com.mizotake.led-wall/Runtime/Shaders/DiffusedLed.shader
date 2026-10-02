Shader "Mizotake/LED Wall/Diffused LED"
{
    Properties
    {
        [MainTexture] _BaseMap("Video / Texture", 2D) = "black" {}
        [NoScaleOffset] _BlendMap("Second Video / Texture", 2D) = "black" {}
        _Transition("Crossfade A to B", Range(0, 1)) = 0
        [HDR] _Tint("Emission Tint", Color) = (1, 1, 1, 1)
        _HousingColor("Black LED Housing", Color) = (0.006, 0.008, 0.012, 1)
        _LedResolution("LED Columns / Rows", Vector) = (160, 90, 0, 0)
        _ContentRect("Content UV Scale / Offset", Vector) = (1, 1, 0, 0)
        _Brightness("HDR Brightness", Range(0, 20)) = 2
        _RgbSeparation("RGB Subpixels", Range(0, 1)) = 0
        _Fill("LED Fill", Range(0.2, 0.95)) = 0.72
        _Diffusion("Optical Diffusion", Range(0, 1)) = 0.65
        _ViewingAngle("Off Axis Dimming", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Diffused LED"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LedVertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "LedCommon.hlsl"
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 cell, footprint;
                half3 video = LedContent(input.uv, cell, footprint);
                float2 p = cell - 0.5;
                float radius = lerp(_Fill * 0.48, 0.62, _Diffusion);
                float aa = max(max(footprint.x, footprint.y) * 0.5, 0.005);
                half lens = 1.0 - smoothstep(radius - aa, radius + aa, length(p));
                half halo = exp(-dot(p, p) / max(radius * radius * 0.8, 0.01));
                float resolved = 1.0 - smoothstep(0.35, 1.0, max(footprint.x, footprint.y));
                half3 emission = video * lerp(1.0, lerp(lens * 1.65, halo * 1.7, _Diffusion), resolved);
                return half4(LedFinish(input, emission, lerp(1.0h, lens, resolved)), 1.0h);
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

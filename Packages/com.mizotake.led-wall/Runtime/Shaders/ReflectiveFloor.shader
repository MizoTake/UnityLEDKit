Shader "Mizotake/LED Wall/Reflective Floor"
{
    Properties
    {
        [MainColor] _BaseColor("Floor Color", Color) = (0.035, 0.042, 0.052, 1)
        _Metallic("Metallic", Range(0, 1)) = 0.25
        _Smoothness("Smoothness", Range(0, 1)) = 0.78
        _ReflectionStrength("Planar Reflection", Range(0, 1)) = 0.25
        _ReflectionRoughness("Reflection Roughness", Range(0, 1)) = 0.25
        _TileSize("Tile Size (World Units)", Float) = 2
        _GroutWidth("Tile Joint Width", Range(0, 0.03)) = 0.004
        _GroutColor("Tile Joint Color", Color) = (0.008, 0.01, 0.014, 1)
        [HideInInspector] _PlanarReflectionTexture("Reflection", 2D) = "black" {}
        [HideInInspector] _ReflectionValid("Reflection Valid", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_PlanarReflectionTexture);
        SAMPLER(sampler_PlanarReflectionTexture);
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _GroutColor;
        float4x4 _PlanarReflectionVP;
        float _Metallic;
        float _Smoothness;
        float _ReflectionStrength;
        float _ReflectionRoughness;
        float _ReflectionValid;
        float _TileSize;
        float _GroutWidth;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half fog : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.fog = ComputeFogFactor(position.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "Lit Reflective Floor"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 tiled = input.positionWS.xz / max(_TileSize, 0.01);
                float2 edge = min(frac(tiled), 1.0 - frac(tiled));
                float aa = max(max(fwidth(tiled.x), fwidth(tiled.y)), 0.0001);
                half grout = 1.0 - smoothstep(_GroutWidth, _GroutWidth + aa, min(edge.x, edge.y));
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = view;
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(normal);
                lighting.vertexLighting = VertexLighting(input.positionWS, normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_BaseColor.rgb, _GroutColor.rgb, grout);
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness * (1.0 - grout * 0.4);
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;
                half3 result = UniversalFragmentPBR(lighting, surface).rgb;
                float4 projected = mul(_PlanarReflectionVP, float4(input.positionWS, 1));
                float2 uv = projected.xy / max(projected.w, 0.0001) * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0) * step(0.0001, projected.w);
                float borderFade = saturate(min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)) * 30.0);
                half fresnel = 0.04h + 0.96h * pow(1.0h - saturate(dot(normal, view)), 5.0h);
                half weight = _ReflectionStrength * _ReflectionValid * inside * borderFade * lerp(0.5h, 1.0h, fresnel) * (1.0h - grout * 0.45h);
                half3 reflection = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, saturate(uv), _ReflectionRoughness * 5.0).rgb;
                result = result * (1.0h - weight * 0.35h) + reflection * weight;
                return half4(MixFog(result, input.fog), 1);
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
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}

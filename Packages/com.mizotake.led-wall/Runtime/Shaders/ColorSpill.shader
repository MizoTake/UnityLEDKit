Shader "Hidden/Mizotake/LED Wall/Color Spill"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Diffuse Color Spill"
            ZWrite Off ZTest Always Cull Off Blend One One ColorMask RGB
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            TEXTURE2D_X(_ReceiverMask);
            TEXTURE2D(_EmissionColors);
            float4 _EmitterCenter, _EmitterRight, _EmitterUp, _EmitterNormal, _EmitterGrid, _SpillSettings;
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(id);
                output.uv = GetFullScreenTriangleTexCoord(id);
                return output;
            }

            float Visibility(float3 receiver, float3 emitter)
            {
                if (_SpillSettings.w <= 0) return 1;
                float visibility = 1;
                [unroll] for (int step = 1; step <= 3; step++)
                {
                    float4 clip = TransformWorldToHClip(lerp(receiver, emitter, step * 0.25));
                    if (clip.w <= 0) continue;
                    float2 uv = clip.xy / clip.w * float2(0.5, 0.5 * _ProjectionParams.x) + 0.5;
                    if (any(uv < 0) || any(uv > 1)) continue;
                    uv = ClampAndScaleUVForBilinear(UnityStereoTransformScreenSpaceTex(uv), _CameraDepthTexture_TexelSize.xy);
                    float scene = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
                    float ray = clip.z / clip.w;
                    #if UNITY_REVERSED_Z
                        bool blocked = scene > ray + 0.001;
                    #else
                        ray = (ray - UNITY_NEAR_CLIP_VALUE) / (1 - UNITY_NEAR_CLIP_VALUE);
                        bool blocked = scene < ray - 0.001;
                    #endif
                    if (blocked) visibility = 1 - _SpillSettings.w;
                }
                return visibility;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (SAMPLE_TEXTURE2D_X(_ReceiverMask, sampler_PointClamp, input.uv).r < 0.5) return 0;
                float depth = SampleSceneDepth(input.uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 position = ComputeWorldSpacePosition(input.uv, depth, UNITY_MATRIX_I_VP);
                float3 normal = normalize(SampleSceneNormals(input.uv));
                float3 irradiance = 0;
                float radius = _SpillSettings.x;
                float softSquared = _SpillSettings.y * _SpillSettings.y;
                [loop] for (int y = 0; y < (int)_EmitterGrid.y; y++)
                [loop] for (int x = 0; x < (int)_EmitterGrid.x; x++)
                {
                    float2 cell = (float2(x, y) + 0.5) / _EmitterGrid.xy - 0.5;
                    float3 source = _EmitterCenter.xyz + _EmitterRight.xyz * cell.x + _EmitterUp.xyz * cell.y;
                    float3 delta = source - position;
                    float distanceSquared = dot(delta, delta);
                    if (distanceSquared >= radius * radius) continue;
                    float3 direction = delta * rsqrt(max(distanceSquared, 0.0001));
                    float cosine = saturate(dot(normal, direction)) * saturate(dot(_EmitterNormal.xyz, -direction));
                    if (cosine <= 0) continue;
                    float falloff = saturate(1 - distanceSquared / (radius * radius));
                    float3 color = LOAD_TEXTURE2D(_EmissionColors, int2(x, y)).rgb;
                    irradiance += color * (cosine * falloff * falloff * _EmitterGrid.z / (distanceSquared + softSquared)) * Visibility(position + normal * 0.03, source);
                }
                return half4(irradiance * (_SpillSettings.z / PI), 0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Opaque Receiver Mask"
            ZWrite Off ZTest Equal Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return 1; }
            ENDHLSL
        }
    }
}

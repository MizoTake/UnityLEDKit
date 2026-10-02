Shader "Hidden/Mizotake/LED Wall/Lighting Sampler"
{
    Properties
    {
        [HideInInspector] _MainTex("Source A", 2D) = "black" {}
        [HideInInspector] _BlendMap("Source B", 2D) = "black" {}
        [HideInInspector] _Transition("Crossfade", Float) = 0
        [HideInInspector] _ContentRect("Content Rect", Vector) = (1, 1, 0, 0)
        [HideInInspector] _CellSize("Cell Size", Vector) = (0.25, 0.5, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _BlendMap;
            float _Transition;
            float4 _ContentRect;
            float4 _CellSize;
            half4 Fragment(v2f_img input) : SV_Target
            {
                half3 color = 0;
                // Nine taps average each emitter region; no recognizable video image is projected onto receivers.
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 uv = (input.uv + float2(x, y) * _CellSize.xy * 0.3) * _ContentRect.xy + _ContentRect.zw;
                        color += lerp(tex2D(_MainTex, uv).rgb, tex2D(_BlendMap, uv).rgb, saturate(_Transition));
                    }
                }
                return half4(color / 9.0, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

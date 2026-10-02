Shader "Hidden/Mizotake/LED Wall/Lighting Sampler"
{
    Properties
    {
        [HideInInspector] _MainTex("Source A", 2D) = "black" {}
        [HideInInspector] _BlendMap("Source B", 2D) = "black" {}
        [HideInInspector] _Transition("Crossfade", Float) = 0
        [HideInInspector] _ContentRect("Content Rect", Vector) = (1, 1, 0, 0)
        [HideInInspector] _Tint("Linear Tint", Vector) = (1, 1, 1, 1)
        [HideInInspector] _ReductionStep("Reduction Step", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CookieAspect("Cookie Aspect", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        sampler2D _BlendMap;
        float _Transition;
        float4 _Tint;
        float4 _ContentRect;
        float4 _ReductionStep;
        float _CookieAspect;
        ENDHLSL
        Pass
        {
            Name "Linear content"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            half4 Fragment(v2f_img input) : SV_Target
            {
                float2 uv = input.uv * _ContentRect.xy + _ContentRect.zw;
                return half4(max(lerp(tex2D(_MainTex, uv).rgb, tex2D(_BlendMap, uv).rgb, saturate(_Transition)) * _Tint.rgb, 0), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Area reduction"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Reduce
            half4 Reduce(v2f_img input) : SV_Target
            {
                float2 d = _ReductionStep.xy;
                return (tex2D(_MainTex, input.uv + d) + tex2D(_MainTex, input.uv - d) + tex2D(_MainTex, input.uv + float2(d.x, -d.y)) + tex2D(_MainTex, input.uv + float2(-d.x, d.y))) * 0.25;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Soft emitter cookie"
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Cookie
            half4 Cookie(v2f_img input) : SV_Target
            {
                float2 p = (input.uv * 2 - 1) * float2(max(1, 1 / _CookieAspect), max(1, _CookieAspect));
                float r2 = dot(p, p);
                half weight = exp(-r2 * 1.2) * (1 - smoothstep(0.65, 1, r2));
                return half4(weight, weight, weight, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

Shader "Hidden/Mizotake/LED Wall/Reflection Blur"
{
    Properties
    {
        [HideInInspector] _MainTex("Source", 2D) = "black" {}
        [HideInInspector] _BlurDirection("Blur Direction", Vector) = (0, 0, 0, 0)
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
            float4 _BlurDirection;
            half4 Fragment(v2f_img input) : SV_Target
            {
                half4 result = tex2D(_MainTex, input.uv) * 0.227027;
                result += tex2D(_MainTex, input.uv + _BlurDirection.xy * 1.384615) * 0.316216;
                result += tex2D(_MainTex, input.uv - _BlurDirection.xy * 1.384615) * 0.316216;
                result += tex2D(_MainTex, input.uv + _BlurDirection.xy * 3.230769) * 0.070270;
                result += tex2D(_MainTex, input.uv - _BlurDirection.xy * 3.230769) * 0.070270;
                return result;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

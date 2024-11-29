Shader "URPShader/BlitTexture"
{
    Properties
    {
        _MainTex ("Base(RGB)", 2D) = "white" {}
        _BaseColor("BaseColor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        
        ZTest Always Cull Off ZWrite Off 

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        CBUFFER_START(UnityPerMaterial)

        float4 _MainTex_ST;
        half4 _BaseColor;

        CBUFFER_END

        SamplerState sampler_BlitTexture;
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        ENDHLSL

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            half4 frag (Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord ) * _BaseColor;
            }
            ENDHLSL
        }
    }
}
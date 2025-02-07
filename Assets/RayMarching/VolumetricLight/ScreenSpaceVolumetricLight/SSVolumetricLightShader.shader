Shader "URPShader/VolumetricLight/SSVolumetricLightShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScreenLightPos ("Screen Light Position", Vector) = (0.5, 0.5, 0, 0)
        _NUM_SAMPLES ("Number of Samples", Float) = 20
        _Density ("Density", Float) = 0.8
        _Exposure ("Exposure", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Renderpipeline"="UniversalPipeline" }
        
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        CBUFFER_START(UnityPerMaterial)

        float4 _MainTex_ST;
        half4 _BaseColor;
        float2 _ScreenLightPos;
        float _LightAttenuation;
        float _NUM_SAMPLES;
        float _Density;
        float _Exposure;
        
        CBUFFER_END

        SamplerState sampler_BlitTexture;
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_CameraOpaqueTexture);
        SAMPLER(sampler_CameraOpaqueTexture);

        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            half GetNoise (half2 uv)
            {
                return frac(sin(dot(uv.xy, half2(12.9898,78.233) ) ) * 43758.5453123);
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 cameraColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.texcoord);
                float2 rayDir = _ScreenLightPos.xy - input.texcoord;                
                float noiseOffset = GetNoise(input.texcoord) * 0.035;
                
                half4 color = half4(0.0f, 0.0f, 0.0f, 1.0f);
                float inverse_NUM_SAMPLES = 1 / (_NUM_SAMPLES - 1) * _Density;
                for(int i = 0; i < _NUM_SAMPLES; i++)
                {
                    float scale = (float)i * inverse_NUM_SAMPLES;
                    scale += noiseOffset;
                    color.xyz += saturate(SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearRepeat, input.texcoord + rayDir * scale).xyz);
                }
                color.xyz = color.xyz / _NUM_SAMPLES;
                return ( color * _LightAttenuation + cameraColor ) * _Exposure;
            }
            ENDHLSL
        }
    }
}

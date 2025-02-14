Shader "URPShader/VolumetricLight/RayMarchingVolumetricLightShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1.0
        _NUM_SAMPLES ("Number of Samples", Float) = 20
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Renderpipeline"="UniversalPipeline" }
        
        HLSLINCLUDE

        #define _MAIN_LIGHT_SHADOWS
        #define _MAIN_LIGHT_SHADOWS_CASCADE
        //#define MAIN_LIGHT_CALCULATE_SHADOWS
        #define random(seed) sin(seed * 641.5467987313875 + 1.943856175)

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" 

        struct CustomAttributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct CustomVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
            float4 screenPos : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        CBUFFER_START(UnityPerMaterial)

        float4 _MainTex_ST;
        float _NUM_SAMPLES;
        float _Intensity;
        float _BlurRange;

        CBUFFER_END
        
        SamplerState sampler_BlitTexture;
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_OriginTex);
        SAMPLER(sampler_OriginTex);
        TEXTURE2D(_CameraOpaqueTexture);
        SAMPLER(sampler_CameraOpaqueTexture);
        
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #define MAX_RAY_LENGTH 50

            float GetLightAttenuation(float3 position)
            {
                float4 shadowPos = TransformWorldToShadowCoord(position);
                float intensity = MainLightRealtimeShadow(shadowPos);
                return intensity;
            }

            half GetNoise (half2 uv)
            {
                return frac(sin(dot(uv.xy, half2(12.9898,78.233) ) ) * 43758.5453123);
            }
            
            CustomVaryings vert(CustomAttributes input)
            {
                CustomVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);

                output.positionCS = pos;
                output.texcoord = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);
                output.screenPos = ComputeScreenPos(pos); 

                return output;
            }

            half4 frag (CustomVaryings input) : SV_Target
            {
                half4 cameraColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord);
                half3 startPosWS = _WorldSpaceCameraPos;
                half2 screenPosUV = input.screenPos.xy / input.screenPos.w;
                //screenPosUV = input.texcoord;
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(screenPosUV);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif

                half3 worldPos = ComputeWorldSpacePosition(screenPosUV, depth, UNITY_MATRIX_I_VP);
                half3 rayDirWS = normalize(worldPos - startPosWS);
                half3 rayLength = length(worldPos - startPosWS);
                rayLength = min(rayLength, MAX_RAY_LENGTH);

                half3 step = rayDirWS * rayLength / _NUM_SAMPLES;
                half3 intensity = 0;

                float seed = random((_ScreenParams.y * screenPosUV.y + screenPosUV.x) * _ScreenParams.x + 0.2);
                float2 noiseUV = screenPosUV * seed;
                float jitterAmount = 1.0 / _NUM_SAMPLES * 0.8;
                for(int i = 0; i < _NUM_SAMPLES; i++)
                {
                    seed = random(seed);
                    float jitter = (seed * 2.0 - 1.0) * jitterAmount;
                    startPosWS += step * (1.0 + jitter);
                    
                    // float jitter = GetNoise(noiseUV) * 0.6;
                    // jitter = (jitter * 2.0 - 1.0) * (1.0 / _NUM_SAMPLES);
                    // startPosWS += step * (1.0 + jitter);

                    //startPosWS += step;
                    float atten = GetLightAttenuation(startPosWS) * _Intensity;
                    half3 light = atten;
                    intensity += light;
                }
                intensity /= _NUM_SAMPLES;
                //return half4(intensity + cameraColor, 1);
                return half4(intensity, 1);
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            half4 frag (CustomVaryings input) : SV_Target
            {
                //KawaseBlur
                float2 res = _BlitTexture_TexelSize.xy;
                float range = _BlurRange;

                half4 col;                
                col.rgb = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord ).rgb;
                col.rgb += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord + float2(range, range) * res).rgb;
                col.rgb += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord + float2(range, -range) * res).rgb;
                col.rgb += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord + float2(-range, range) * res).rgb;
                col.rgb += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord + float2(-range, -range) * res).rgb;
                col.rgb /= 5.0f;
                return col; 
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            half4 frag (CustomVaryings input) : SV_Target
            {
                half4 cameraColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.texcoord);
                half4 blurTex = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord );
                return half4(cameraColor.rgb + blurTex.rgb, 1); 
            }
            ENDHLSL
        }
    }
}

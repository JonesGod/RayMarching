Shader "Unlit/ReflectionProbeTest"
{
    Properties
    {
        _Reflection_CubeMap("_Reflection_CubeMap", Cube) = "_Skybox"{}
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };
            
            samplerCUBE _Reflection_CubeMap;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;

                half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
                output.viewDirWS = viewDirWS;

                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenPosUV = input.screenPos.xy / input.screenPos.w;
                half3 reflectColor;
                half3 reflectDir = reflect(-input.viewDirWS, input.normalWS);
                #if defined(_REFLECTION_PROBE_BLENDING) || USE_FORWARD_PLUS
                    reflectColor = CalculateIrradianceFromReflectionProbes(reflectDir, input.positionWS, 0, screenPosUV);
                #else
                    reflectColor = DecodeHDREnvironment(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, 0.0), unity_SpecCube0_HDR);
                #endif
                //float3 cubeRCol = texCUBE(_Reflection_CubeMap, reflectDir).rgb;;
                
                return half4(reflectColor, 1.0);
            }

            ENDHLSL
        }
    }
}
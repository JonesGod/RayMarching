Shader "URPShader/RayMarchingShader"
{
    Properties
    {
        _MainTex ("Base(RGB)", 2D) = "white" {}
        _BaseColor("BaseColor", Color) = (1,1,1,1)
         _SphereCenter ("Sphere Center", Vector) = (0, 0, 0)
        _SphereRadius ("Sphere Radius", Float) = 1.0
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        
        ZTest Always Cull Off ZWrite Off 

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)

        float4 _MainTex_ST;
        half4 _BaseColor;
        float3 _SphereCenter;
        float _SphereRadius;
        float4 _BackgroundColor;

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

            #define MAX_STEPS 100
            #define MAX_DISTANCE 50.0
            #define DIST_TO_SURF 0.001

            float SphereSDF(float3 pointWS)
            {
                return length(pointWS - _SphereCenter) - _SphereRadius;
            }

            float Raymarch(float3 origin, float3 direction, out float3 hitPoint)
            {
                const float EPSILON = 0.001;
                const float MAX_DIST = 50.0;

                float distanceTraveled = 0.0;
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 currentPos = origin + direction * distanceTraveled;
                    float distToSurface = SphereSDF(currentPos);

                    if (distToSurface < EPSILON)
                    {
                        hitPoint = currentPos;
                        return distanceTraveled;
                    }

                    if (distanceTraveled > MAX_DIST)
                        break;

                    distanceTraveled += distToSurface;
                }

                hitPoint = float3(0, 0, 0);
                return -1.0; // No hit
            }
            
            // Calculate normal at the surface using gradient approximation
            float3 CalculateNormal(float3 pointWS)
            {
                const float delta = 0.001;
                float3 dx = float3(delta, 0, 0);
                float3 dy = float3(0, delta, 0);
                float3 dz = float3(0, 0, delta);

                float nx = SphereSDF(pointWS + dx) - SphereSDF(pointWS - dx);
                float ny = SphereSDF(pointWS + dy) - SphereSDF(pointWS - dy);
                float nz = SphereSDF(pointWS + dz) - SphereSDF(pointWS - dz);

                return normalize(float3(nx, ny, nz));
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 UV = input.positionCS.xy / _ScaledScreenParams.xy;
                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(UV);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif
                
                // Reconstruct the world space positions.
                float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
                float3 rayDir = normalize(worldPos - _WorldSpaceCameraPos);
                
                float3 hitPoint;
                float hitDistance = Raymarch(_WorldSpaceCameraPos, rayDir, hitPoint);
                if (hitDistance > 0.0)
                {
                    // Calculate normal
                    float3 normal = CalculateNormal(hitPoint);

                    // Lambert lighting
                    Light light = GetMainLight();
                    float lightIntensity = max(0.0, dot(normal, normalize(light.direction)));

                    // Combine base color with lighting
                    float3 color = _BaseColor.rgb * lightIntensity;
                    return float4(color, 1.0);
                }
                else
                {
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord );
                }
            }
            ENDHLSL
        }
    }
}
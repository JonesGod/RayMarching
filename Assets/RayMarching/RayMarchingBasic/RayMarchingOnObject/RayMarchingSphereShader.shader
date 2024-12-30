Shader "URPShader/RayMarchingSphereShader"
{
    Properties
    {
        _SphereCenter ("Sphere Center", Vector) = (0, 0, 0)
        _SphereRadius ("Sphere Radius", Float) = 0.55
        _MainTex ("Base(RGB)", 2D) = "white" {}
        _BaseColor("BaseColor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)

        float3 _SphereCenter;
        float _SphereRadius;
        float4 _MainTex_ST;
        half4 _BaseColor;

        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 uv : TEXCOORD1;                
        };

        ENDHLSL

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #define MAX_STEPS 100
            #define MAX_DISTANCE 50.0
            #define DIST_TO_SURF 0.001
            
            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                return output;
            }

            float SphereSDF(float3 rayPoint)
            {
                return length(rayPoint - _SphereCenter) - _SphereRadius;
            }

            float3 GetNormalCentralDifferences(float3 pointWS)
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

            float RayMarch(float3 rayOrigin, float3 rayDir, out float3 hitPoint)
            {
                float distanceTraveled = 0;
                for ( int i = 0; i < MAX_STEPS; ++i)
                {
                    float3 currentPos = rayOrigin + distanceTraveled * rayDir;
                    float distanceFromSurface = SphereSDF(currentPos);
                    if ( distanceFromSurface < DIST_TO_SURF )
                    {
                        hitPoint = currentPos;
                        return distanceTraveled;
                    }
                    if( distanceTraveled > MAX_DISTANCE )
                    {
                        break;
                    }
                    distanceTraveled += distanceFromSurface;
                }
            
                hitPoint = float3(0, 0, 0);    
                return MAX_DISTANCE;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 worldPos = input.positionWS;
                float3 rayDir = normalize(worldPos - _WorldSpaceCameraPos);

                float4 finalColor = float4(1, 0, 0, 1);

                float3 hitPoint;
                float distance = RayMarch(_WorldSpaceCameraPos, rayDir, hitPoint);
                if( distance < MAX_DISTANCE )
                {
                    float3 normal = GetNormalCentralDifferences(hitPoint);

                    Light light = GetMainLight();
                    finalColor.rgb = max(0.0, dot(normal, normalize(light.direction)));
                }
                return finalColor;
            }
            ENDHLSL
        }
    }
}
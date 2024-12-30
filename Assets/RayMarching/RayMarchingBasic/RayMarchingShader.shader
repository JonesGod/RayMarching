Shader "URPShader/RayMarchingShader"
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "DistanceFunctions.hlsl"

        struct Sphere
        {
            float3 position;
            float radius;
            float3 color;
        };

        CBUFFER_START(UnityPerMaterial)

        float4 _MainTex_ST;
        half4 _BaseColor;
        float3 _SphereCenter;
        uniform float4x4 _CameraRayMatrix, _CamToWorldMatrix;
        uniform float4 _Spheres[2];

        CBUFFER_END
        
        StructuredBuffer<Sphere> _SphereBuffer;
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
            float2 uv : TEXCOORD0;
            float3 rayWS : TEXCOORD1;
            float4 screenPos : TEXCOORD2;
        };

        ENDHLSL

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #define MAX_STEPS 101
            #define MAX_DISTANCE 30.0
            #define EPSILON 0.01

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;

                int index = (int)dot(input.texcoord, half2(1,2));
                output.rayWS = mul(_CamToWorldMatrix, _CameraRayMatrix[index].xyz);

                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            float DistanceField(float3 pointWS)
            {
                float result = PlaneSDF(pointWS, float3(0, 1, 0), 0);
                for ( int i = 0; i < 2; ++i)
                {
                    float sphereSDF = SphereSDF(pointWS - _Spheres[i].xyz, _Spheres[i].w);
                    result = opSmoothUnion2(result, sphereSDF, 0.35);
                }

                return result;
            }

            float Raymarch(float3 origin, float3 direction, float eyeDepth, out float3 hitPoint)
            {
                float distanceTraveled = 0.0;
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    if (distanceTraveled > MAX_DISTANCE || distanceTraveled >= eyeDepth)
                        break;
                    
                    float3 currentPos = origin + direction * distanceTraveled;
                    float distToSurface = DistanceField(currentPos);

                    if (distToSurface < EPSILON)
                    {
                        hitPoint = currentPos;
                        return distanceTraveled;
                    }
                    // Move along the ray
                    distanceTraveled += distToSurface;

                }

                hitPoint = float3(0, 0, 0);
                return -1.0; // No hit
            }
            
            // Calculate normal at the surface using gradient approximation
            //---------------------------------------------------------------------------
            float3 CalculateNormal(float3 pointWS)
            {
                const float delta = 0.001;
                float3 dx = float3(delta, 0, 0);
                float3 dy = float3(0, delta, 0);
                float3 dz = float3(0, 0, delta);
                
                float nx = DistanceField(pointWS + dx) - DistanceField(pointWS - dx);
                float ny = DistanceField(pointWS + dy) - DistanceField(pointWS - dy);
                float nz = DistanceField(pointWS + dz) - DistanceField(pointWS - dz);
                
                return normalize(float3(nx, ny, nz));
            }
            //----------------------------------------------------------------------------

            // Caculate Shadow
            //---------------------------------------------------------------------------
            float HardShadow(float3 ro,float3 rd,float mint,float maxt)
            {
                float t = mint;
                for( int i = 0; i < 256 && t < maxt; i++ )
                {
                    float h = DistanceField(ro + rd * t);
                    if( h < 0.001f)
                        return 0.0f;
                    t += h;
                }
                return 1.0f;
            }

            float CaculateShadow( float3 rayOrign, float3 rayDir, float mint, float maxt, float k )
            {
                float res = 1.0;
                float t = mint;
                for( int i=0; i < 256 && t < maxt; i++ )
                {
                    float h = DistanceField(rayOrign + rayDir * t);
                    if( h < 0.001 ) //Has Contact
                        return 0.0;
                    res = min( res, k*h/t );
                    t += h;
                }
                return res;
            }
            //----------------------------------------------------------------------------

            // Caculate AO
            //---------------------------------------------------------------------------
            float CaculateAO( float3 rayOrigin, float3 normal)
            {
                float step = 0.1;
                float ao = 0.0;
                float dist = 0;
                for(int i = 0; i < 3; ++i)
                {
                    dist = step * i;
                    //if(dist - DistanceField(rayOrigin + normal * dist) > 0) => has contact
                    //why / dist => The less the dist, the higher the contribution
                    ao += max(0.0f, (dist - DistanceField(rayOrigin + normal * dist) ) / dist);
                }
                return saturate(1.0f - ao) * 0.5 + 0.5;
            }
            //---------------------------------------------------------------------------

            float3 Shading( float3 hitPoint, float3 hitColor )
            {
                float3 normal = CalculateNormal(hitPoint);

                // Lambert lighting
                Light light = GetMainLight();
                float lightIntensity = max(0.0, dot(normal, normalize(light.direction) ) * 0.5 + 0.5);

                float shadow = CaculateShadow(hitPoint, light.direction, 0.01, 30, 3) * 0.5 + 0.5;
                float ao = CaculateAO(hitPoint, normal);

                float3 ambient = _GlossyEnvironmentColor.xyz;;
                //return shadow;
                // Combine base color with lighting
                float3 color = hitColor * shadow * lightIntensity * ao;
                return color;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 screenPos = input.screenPos.xy / input.screenPos.w;
                float2 UV = input.positionCS.xy / _ScaledScreenParams.xy;
                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(screenPos);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif
                float eyeDepth = LinearEyeDepth(depth, _ZBufferParams);
                
                // Reconstruct the world space positions.
                //float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
                //float3 rayDir = normalize(worldPos - _WorldSpaceCameraPos);
                float3 rayDir = normalize(input.rayWS);
                
                float3 hitPoint;
                float hitDistance = Raymarch(_WorldSpaceCameraPos, rayDir, eyeDepth, hitPoint);
                //return eyeDepth;
                if ( hitDistance >= 0.0 )
                {
                    // Combine base color with lighting
                    float3 color = Shading(hitPoint, _BaseColor.rgb);
                    return float4(color, 1.0);
                }
                else
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv );
                }
            }
            ENDHLSL
        }
    }
}
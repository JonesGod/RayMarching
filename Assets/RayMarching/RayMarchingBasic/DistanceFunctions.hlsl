#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float SphereSDF(float3 positionWS, float radius)
{
    return length(positionWS) - radius;
}

float PlaneSDF( float3 positionWS, float3 normal, float height )
{
  // n must be normalized
  return dot(positionWS, normal.xyz) + height;
}

float4 OpSmoothUnion( float4 d1, float4 d2, float k )
{
    float h = clamp( 0.5 + 0.5*(d2.w - d1.w) / k, 0.0, 1.0 );
    float3 color = lerp(d2.rgb, d1.rgb, h);
    float dist = lerp(d2.w, d1.w, h ) - k*h*(1.0 - h);
    return float4(color, dist);
}

// float4 OpSmoothUnion( float4 d1, float4 d2, float k )
// {
//     float h = max(k-abs(d1.w-d2.w),0.0);
//     float3 color = lerp(d2.rgb, d1.rgb, h);
//     float dist = min(d1.w, d2.w) - h*h*0.25/k;
//     return float4(color, dist);
// }

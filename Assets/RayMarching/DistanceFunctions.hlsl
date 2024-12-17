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

float opSmoothUnion( float d1, float d2, float k )
{
    float h = clamp( 0.5 + 0.5*(d2 - d1) / k, 0.0, 1.0 );
    return lerp( d2, d1, h ) - k*h*(1.0 - h);
}

float opSmoothUnion2( float d1, float d2, float k )
{
    float h = max(k-abs(d1-d2),0.0);
    return min(d1, d2) - h*h*0.25/k;
}

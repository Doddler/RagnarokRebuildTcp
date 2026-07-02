#ifndef RO_SHADOW_CASTER_INCLUDED
#define RO_SHADOW_CASTER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct RoShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
struct RoShadowVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

float4 RoGetShadowPositionHClip(RoShadowAttributes v)
{
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

RoShadowVaryings RoShadowVert(RoShadowAttributes v)
{
    RoShadowVaryings o;
    o.positionCS = RoGetShadowPositionHClip(v);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

half4 RoShadowFrag(RoShadowVaryings i) : SV_Target
{
    clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
    return 0;
}
#endif

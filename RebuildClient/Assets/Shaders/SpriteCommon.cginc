#ifndef SPRITE_COMMON_INCLUDED
#define SPRITE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "RoAdditionalLights.hlsl"
#include "billboard.cginc"

float4 _MainTex_TexelSize;
half _VPos;

#ifdef DYNBATCH_ON
TEXTURE2D_ARRAY(_AtlasArray);
SAMPLER(sampler_AtlasArray);
static const float _AtlasArraySize = 2048;
#endif

float4 SpriteComputeScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o;
}

float3 SpriteAdditionalLight(uint loopIndex, float3 positionWS)
{
    uint lightIndex = RoResolveAdditionalLightIndex(loopIndex);
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    half3 color = _AdditionalLightsBuffer[lightIndex].color.rgb;
    float w = _AdditionalLightsBuffer[lightIndex].position.w;
#else
    half3 color = _AdditionalLightsColor[lightIndex].rgb;
    float w = _AdditionalLightsPosition[lightIndex].w;
#endif
    return color * (RoSoftAdditionalAttenuation(loopIndex, positionWS) * w);
}

float3 ShadeVertexLightsSprite(float3 positionWS)
{
    float3 lightColor = unity_AmbientSky.rgb;
    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    uint count = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    float4 sp = SpriteComputeScreenPos(TransformWorldToHClip(positionWS));
    inputData.normalizedScreenSpaceUV = sp.xy / max(sp.w, 1e-5);
    #endif
    LIGHT_LOOP_BEGIN(count)
        lightColor += SpriteAdditionalLight(lightIndex, positionWS);
    LIGHT_LOOP_END
    #endif
    return lightColor;
}

#endif

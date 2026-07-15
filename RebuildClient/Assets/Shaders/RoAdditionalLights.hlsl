#ifndef RO_ADDITIONAL_LIGHTS_INCLUDED
#define RO_ADDITIONAL_LIGHTS_INCLUDED


uint RoResolveAdditionalLightIndex(uint loopIndex)
{
#if USE_CLUSTER_LIGHT_LOOP
    return loopIndex;
#else
    return (uint)GetPerObjectLightIndex(loopIndex);
#endif
}

half RoSoftAdditionalAttenuation(uint loopIndex, float3 positionWS)
{
    uint lightIndex = RoResolveAdditionalLightIndex(loopIndex);
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 lightPositionWS = _AdditionalLightsBuffer[lightIndex].position;
    half4 attenParams      = _AdditionalLightsBuffer[lightIndex].attenuation;
    half4 spotDirection    = _AdditionalLightsBuffer[lightIndex].spotDirection;
#else
    float4 lightPositionWS = _AdditionalLightsPosition[lightIndex];
    half4 attenParams      = _AdditionalLightsAttenuation[lightIndex];
    half4 spotDirection    = _AdditionalLightsSpotDir[lightIndex];
#endif
    float3 toLight = lightPositionWS.xyz - positionWS * lightPositionWS.w;
    float distSq = max(dot(toLight, toLight), 1e-6);
    half distanceAtten = rcp(1.0 + distSq * attenParams.x);

    half3 lightDir = half3(toLight * rsqrt(distSq));
    half spotAtten = AngleAttenuation(spotDirection.xyz, lightDir, attenParams.zw);

    return distanceAtten * spotAtten;
}

half4 RoFragmentPBRSoft(InputData inputData, SurfaceData surfaceData)
{
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = GlobalIllumination(brdfData, inputData.bakedGI, aoFactor.indirectAmbientOcclusion,
                                              inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
    lightingData.mainLightColor = LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        light.distanceAttenuation = RoSoftAdditionalAttenuation(lightIndex, inputData.positionWS);
        lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
    LIGHT_LOOP_END
#endif

    return CalculateFinalColor(lightingData, surfaceData.alpha);
}

#endif

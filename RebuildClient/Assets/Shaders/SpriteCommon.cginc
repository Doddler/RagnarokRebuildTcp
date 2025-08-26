#ifndef SPRITE_COMMON_INCLUDED
#define SPRITE_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "billboard.cginc"

#pragma multi_compile _ GROUND_ITEM

float4 _MainTex_TexelSize;
fixed _VPos;

#ifdef GROUND_ITEM
struct InstanceData
{
    float4 color;
    float4 uvRect;
    float offset;
    float colorDrain;
};

StructuredBuffer<InstanceData> _Instances;
int _BaseInstance;

inline void SetupInstancingData
(
    uint instanceID, uint vertexID,
    inout float3 positionOS,
    inout float2 uv,
    inout float4 vcol,
    inout float4 color,
    inout float isHidden,
    inout float offset,
    inout float colorDrain,
    inout float vPos)
{
    InstanceData inst = _Instances[_BaseInstance + instanceID];

    //positionOS = inst.positionOS[vertexID];
    float4 rect = inst.uvRect;
    uv = rect.xy * _MainTex_TexelSize.xy + uv * rect.zw * _MainTex_TexelSize.xy;

    color = inst.color;
    offset = inst.offset;
    colorDrain = inst.colorDrain;
}
#else
struct InstanceData
{
    float3 positionOS[4];
    float2 uv[4];
    float4 vcol[4];

    float4 color;

    float isHidden;
    float offset;
    float colorDrain;
    float vPos;
};

StructuredBuffer<InstanceData> _Instances;
int _BaseInstance;

inline void SetupInstancingData
(
    uint instanceID, uint vertexID,
    inout float3 positionOS,
    inout float2 uv,
    inout float4 vcol,
    inout float4 color,
    inout float isHidden,
    inout float offset,
    inout float colorDrain,
    inout float vPos)
{
    InstanceData inst = _Instances[_BaseInstance + instanceID];

    positionOS = inst.positionOS[vertexID];
    uv = inst.uv[vertexID];
    vcol = inst.vcol[vertexID];

    color = inst.color;
    isHidden = inst.isHidden;
    offset = inst.offset;
    colorDrain = inst.colorDrain;
    vPos = inst.vPos;
}
#endif

#endif
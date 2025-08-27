#ifndef SPRITE_COMMON_INCLUDED
#define SPRITE_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "billboard.cginc"

float4 _MainTex_TexelSize;
fixed _VPos;

#ifdef GROUND_ITEM
struct InstanceData
{
    float4 color;
    float4 uvRect;
    float offset;
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
    inout float vPos,
    inout float width)
{
    InstanceData inst = _Instances[_BaseInstance + instanceID];

    //positionOS = inst.positionOS[vertexID];
    float4 rect = inst.uvRect;
    uv = rect.xy * _MainTex_TexelSize.xy + uv * rect.zw * _MainTex_TexelSize.xy;

    color = inst.color;
    offset = inst.offset;
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
    float width;
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
    inout float vPos,
    inout float width)
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
    width = inst.width;
}
#endif

float3 ShadeVertexLightsSprite(float3 pos)
{
    float3 viewpos = UnityWorldToViewPos(pos);

    float3 lightColor = UNITY_LIGHTMODEL_AMBIENT.xyz;
    UNITY_UNROLL
    for (int i = 0; i < 8; i++)
    {
        float3 toLight = unity_LightPosition[i].xyz - viewpos.xyz * unity_LightPosition[i].w;

        float lengthSq = dot(toLight, toLight);

        lengthSq = max(lengthSq, 0.000001);
        toLight *= rsqrt(lengthSq);

        float atten = rcp(1.0 + lengthSq * unity_LightAtten[i].z);

        // Spot light support.
        float rho = max(0, dot(toLight, unity_SpotDirection[i].xyz));
        float spotAtt = (rho - unity_LightAtten[i].x) * unity_LightAtten[i].y;
        atten *= saturate(spotAtt);

        // unity_LightPosition[i].w will be 0 for directional lights
        lightColor += unity_LightColor[i].rgb * atten * unity_LightPosition[i].w;
    }
    return lightColor;
}

#endif
#ifndef SPRITE_DEPTH_ONLY_INCLUDED
#define SPRITE_DEPTH_ONLY_INCLUDED

#include "SpriteCommon.cginc"

struct appdata_t
{
    float4 positionOS : POSITION;
    float3 normal : NORMAL;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
    #ifdef DYNBATCH_ON
    float3 anchorWS    : TEXCOORD3;
    float4 cornerOS    : TEXCOORD4;
    float4 spriteColor : TEXCOORD5;
    float4 packed      : TEXCOORD6;
    float2 hiddenX     : TEXCOORD7;
    float  slice       : TEXCOORD1;
    float4 uvRect      : TEXCOORD2;
    #endif
};

struct v2f
{
    float4 pos : SV_POSITION;
    half4 color : TEXCOORD0;
    float2 texcoord : TEXCOORD1;
    #ifdef DYNBATCH_ON
    float slice : TEXCOORD2;
    float4 uvRect : TEXCOORD3;
    #endif
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
half4 _Color;

v2f vert(appdata_t v)
{
    v2f o = (v2f)0;

    #ifdef DYNBATCH_ON
    float vPosShift = v.packed.z;
    float3 cornerOffset = v.cornerOS.xyz + vPosShift * v.normal;
    float3 originOffset = v.positionOS.xyz;
    float posY = v.cornerOS.w + vPosShift;
    Billboard billboard = GetBillboardDB(v.anchorWS, cornerOffset, originOffset, posY, 0);
    o.pos = billboard.positionCS;
    if (v.hiddenX.x > 0.5)
        o.pos = float4(2, 2, 2, 1);
    o.color = v.color * v.spriteColor;
    o.slice = v.slice;
    o.uvRect = v.uvRect;
    #else
    v.positionOS.y += _VPos;
    Billboard billboard = GetBillboard(v.positionOS, 0);
    o.pos = billboard.positionCS;
    o.color = v.color * _Color;
    #endif

    o.texcoord = v.texcoord;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    #ifdef DYNBATCH_ON
    float2 suv = clamp(i.texcoord, i.uvRect.xy, i.uvRect.zw);
    half4 c = SAMPLE_TEXTURE2D_ARRAY_LOD(_AtlasArray, sampler_AtlasArray, suv, i.slice, 0);
    #else
    half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
    #endif
    c *= i.color;

    clip(c.a - 0.5);

    return c;
}

#endif

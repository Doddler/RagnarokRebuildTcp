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
    #ifdef INSTANCING_ON
    uint vid : SV_VertexID;
    uint instanceID : SV_InstanceID;
    #endif
};

struct v2f
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    float2 texcoord : TEXCOORD0;
    #ifdef DYNBATCH_ON
    float slice : TEXCOORD1;
    float4 uvRect : TEXCOORD2;
    #endif
};

sampler2D _MainTex;
fixed4 _Color;

v2f vert(appdata_t v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);

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
    #if defined(INSTANCING_ON) && defined(GROUND_ITEM)
    float isHidden = 0;
    float _Offset = 0;
    float _ColorDrain = 0;
    float _Width = 0;
    SetupInstancingData(v.instanceID, v.vid, v.positionOS.xyz, v.texcoord.xy, v.color, _Color, isHidden, _Offset, _ColorDrain, _VPos, _Width);
    #endif

    v.positionOS.y += _VPos;
    Billboard billboard = GetBillboard(v.positionOS, 0);
    o.pos = billboard.positionCS;
    #if defined(INSTANCING_ON) && defined(GROUND_ITEM)
    o.pos.z += 0.001;
    #endif
    o.color = v.color * _Color;
    #endif

    o.texcoord = v.texcoord;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    #ifdef DYNBATCH_ON
    float2 suv = clamp(i.texcoord, i.uvRect.xy, i.uvRect.zw);
    fixed4 c = UNITY_SAMPLE_TEX2DARRAY_LOD(_AtlasArray, float3(suv, i.slice), 0);
    #else
    fixed4 c = tex2D(_MainTex, i.texcoord);
    #endif
    c *= i.color;

    clip(c.a - 0.5);

    return c;
}


#endif

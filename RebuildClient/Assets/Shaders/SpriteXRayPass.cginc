#ifndef SPRITE_XRAY_INCLUDED
#define SPRITE_XRAY_INCLUDED

#include "SpriteCommon.cginc"

struct appdata_t
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
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
};

sampler2D _MainTex;
fixed4 _Color;
float _Offset;

v2f vert(appdata_t v)
{
    UNITY_SETUP_INSTANCE_ID(v);

    #ifdef INSTANCING_ON
    float isHidden = 0;
    float _Offset = 0;
    float _ColorDrain = 0;
    SetupInstancingData(v.instanceID, v.vid, v.vertex.xyz, v.texcoord.xy, v.color, _Color, isHidden, _Offset, _ColorDrain, _VPos);
    #endif

    v2f o;

    Billboard billboard = GetBillboard(v.vertex, _Offset);
    o.pos = billboard.positionCS;

    o.color = v.color * _Color;
    o.texcoord = v.texcoord;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    fixed4 c = tex2D(_MainTex, i.texcoord);
    c *= i.color;
    c.rgb *= c.a;

    clip(frac(i.pos.x / 2) - 0.5);
    clip(frac(i.pos.y / 2) - 0.5);

    return c;
}

#endif
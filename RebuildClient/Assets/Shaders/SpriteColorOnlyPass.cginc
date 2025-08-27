#ifndef SPRITE_COLOR_ONLY_INCLUDED
#define SPRITE_COLOR_ONLY_INCLUDED

#include "SpriteCommon.cginc"

struct appdata_t
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
    #ifdef INSTANCING_ON
    uint vid : SV_VertexID;
    uint instanceID : SV_InstanceID;
    #endif
};

struct v2f
{
    float4 positionCS : SV_POSITION;
    #if BLINDEFFECT_ON
    fixed4 color : COLOR0;
    fixed4 color2 : COLOR1;
    #else
    fixed4 color : COLOR;
    fixed4 lighting : COLOR2;
    #endif
    float2 texcoord : TEXCOORD0;
    float4 screenPos : TEXCOORD1;
    half4 worldPos : TEXCOORD2;
    UNITY_FOG_COORDS(3)
    UNITY_VERTEX_OUTPUT_STEREO
};

fixed4 _Color;
fixed4 _EnvColor;
fixed _Offset;
fixed _Rotation;
fixed _Width;
fixed _ColorDrain;

float4 _ClipRect;

float3 _LightingSamplePosition;
float _IsMeshRenderer;

sampler2D _MainTex;
float4 _MainTex_ST;
// sampler2D _PalTex;

sampler2D _WaterDepth;
sampler2D _WaterImageTexture;
float4 _WaterImageTexture_ST;

//float _MaskSoftnessX;
//float _MaskSoftnessY;

// Should we use a global CBuffer? would it even be worth adding the complexity?
//from our globals
float4 _RoAmbientColor;
float4 _RoDiffuseColor;

float4 unity_Lightmap_ST;

#ifdef BLINDEFFECT_ON
float4 _RoBlindFocus;
float _RoBlindDistance;
#endif

v2f vert(appdata_t v)
{
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v)
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v)

    #ifdef INSTANCING_ON
    float isHidden = 0;
    SetupInstancingData(v.instanceID, v.vid, v.positionOS.xyz, v.texcoord.xy, v.color, _Color, isHidden, _Offset, _ColorDrain, _VPos, _Width);
    #endif
    
    v.positionOS.y += _VPos;
    Billboard billboard = GetBillboard(v.positionOS, _Offset);
    float3 worldPos = billboard.positionWS;

    o.positionCS = billboard.positionCS;
    o.color = v.color * _Color;
    
    float4 tempVertex = UnityObjectToClipPos(v.positionOS);
    UNITY_TRANSFER_FOG(o, tempVertex);

    //smoothpixelshader stuff here
    #ifdef SMOOTHPIXEL
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    float2 maskUV = (v.positionOS.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
    o.texcoord = float4(v.texcoord.x, v.texcoord.y, maskUV.x, maskUV.y);
    #else
    o.texcoord = v.texcoord;
    #endif

    // It's cheap enougth that we can just perform the calculation for every vertex.
    // For smoother lighting use billboard.positionWS as the sampling pos.
    float3 samplingPos = _IsMeshRenderer > 0.5 ? _LightingSamplePosition: mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    o.lighting = float4(ShadeVertexLightsSprite(samplingPos), 1.0);
    
    #ifndef WATER_OFF

        //this mess fully removes the rotation from the matrix	
        float3 scale = float3(
            length(unity_ObjectToWorld._m00_m10_m20),
            length(unity_ObjectToWorld._m01_m11_m21),
            length(unity_ObjectToWorld._m02_m12_m22)
        );

        // We can't edit unity_ObjectToWorld when instancing is enabled.
        float4x4 mod_ObjectToWorld = unity_ObjectToWorld;
        mod_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
        mod_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
        mod_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

        //build info needed for water line
        worldPos = mul(mod_ObjectToWorld, float4(v.positionOS.x, v.positionOS.y * 1.5, 0, 1)).xyz; //fudge y sprite height 
        o.screenPos = ComputeScreenPos(o.positionCS);
        o.worldPos = float4(v.positionOS.x, worldPos.y, 0, 0);
    #endif

    #if BLINDEFFECT_ON
    float d = distance(worldPos, _RoBlindFocus);
    //d = 1.2 - d / _RoBlindDistance;
    d = 1.5 - (d / _RoBlindDistance) * 1.5 + clamp((_RoBlindDistance - 50) / 120, -0.2, 0);
    o.color2.rgb = clamp(1 * d, -1, 1);
    #endif

    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    #ifdef XRAY
    clip(frac(i.positionCS.x / 2) - 0.5);
    clip(frac(i.positionCS.y / 2) - 0.5);
    #endif
    
    //environment ambient contribution disabled for now as it muddies the sprite
    //todo: turn ambient contribution back on if fog is disabled.
    float4 env = float4(1, 1, 1, 1);
    //return i.color;
    //float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
    //env = env * 0.3 + 0.7;// + saturate(0.5 + i.envColor);

    //smoothpixel
    // apply anti-aliasing
    #ifdef SMOOTHPIXEL
    float2 texturePosition = i.texcoord * _MainTex_TexelSize.zw;
    float2 nearestBoundary = round(texturePosition);
    float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
                        abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

    float2 samplePosition = (texturePosition - nearestBoundary) / delta;
    samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

    fixed4 diff = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);
    #else
    fixed4 diff = tex2D(_MainTex, i.texcoord.xy);

    #endif
    // fixed4 diff = tex2D(_MainTex,i.texcoord.xy);
    //endsmoothpixel

    // //#ifdef PALETTE_ON
    // diff *= 256;
    // diff = floor(diff);
    // diff /= 256;
    // diff = float4(tex2D(_PalTex, float2((diff.r+diff.g+diff.b)/3, 0.5)).rgb, diff.a);
    // //#endif

    // fixed4 lm = UNITY_SAMPLE_TEX2D(unity_Lightmap, i.light.xy);
    // half3 bakedColor = DecodeLightmap(lm);

    //return float4(i.color.rgb/2, 1);

    //The UNITY_APPLY_FOG can't be called twice so we'll store it to re-use later
    float4 fogColor = float4(1, 1, 1, 1);

    float avg = (diff.r + diff.g + diff.b) / 3;
    diff.rgb = lerp(diff.rgb, float3(avg, avg, avg), _ColorDrain);
    diff.rgb += diff * i.lighting;

    fixed4 c = diff * min(1.35, fogColor * i.color * float4(env.rgb, 1));
    c = saturate(c);

    clip(c.a - 0.001);
    
    #ifndef WATER_OFF
    float2 uv = (i.screenPos.xy / i.screenPos.w);
    float4 water = tex2D(_WaterDepth, uv);
    float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);

    if (water.a < 0.1)
        return c;

    float4 waterTex = tex2D(_WaterImageTexture, wateruv);
    float height = water.z;
    
    waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex * 0.6);

    float simHeight = i.worldPos.y - abs(i.worldPos.x) / (_Width) * 0.5;

    simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);
    waterTex *= fogColor;

    if (height - 0 > simHeight)
        c.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));

    #endif

    UNITY_APPLY_FOG(i.fogCoord, c);
    c.rgb *= c.a;

    #ifdef BLINDEFFECT_ON
    c.rgb = saturate(c.rgb * i.color2);
    #endif

    return c;
}

#endif
#ifndef SPRITE_COLOR_ONLY_INCLUDED
#define SPRITE_COLOR_ONLY_INCLUDED

#include "SpriteCommon.cginc"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"

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
    float4 positionCS : SV_POSITION;
    half4 color : TEXCOORD0;
    half4 lighting : TEXCOORD1;
    float2 texcoord : TEXCOORD2;
    float4 screenPos : TEXCOORD3;
    half4 worldPos : TEXCOORD4;
    half fogFactor : TEXCOORD5;
    #if BLINDEFFECT_ON
    half4 color2 : TEXCOORD6;
    #endif
    #ifdef DYNBATCH_ON
    half2 spriteParams : TEXCOORD7;
    float slice : TEXCOORD8;
    float4 uvRect : TEXCOORD9;
    #endif
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

half4 _Color;
half4 _EnvColor;
half _Offset;
half _Rotation;
half _Width;
half _ColorDrain;

float4 _ClipRect;

float3 _LightingSamplePosition;
float _IsMeshRenderer;

TEXTURE2D(_WaterDepth);
SAMPLER(sampler_WaterDepth);
TEXTURE2D(_WaterImageTexture);
SAMPLER(sampler_WaterImageTexture);
float4 _WaterImageTexture_ST;

float4 _RoAmbientColor;
float4 _RoDiffuseColor;

#ifdef BLINDEFFECT_ON
float4 _RoBlindFocus;
float _RoBlindDistance;
#endif

v2f vert(appdata_t v)
{
    v2f o = (v2f)0;

    float3 worldPos;

    #ifdef DYNBATCH_ON
    float3 anchorWS = v.anchorWS;
    float vPosShift = v.packed.z;
    float3 cornerOffset = v.cornerOS.xyz + vPosShift * v.normal;
    float3 originOffset = v.positionOS.xyz;
    float posY = v.cornerOS.w + vPosShift;
    float offset_ = v.packed.y;
    Billboard billboard = GetBillboardDB(anchorWS, cornerOffset, originOffset, posY, offset_);
    worldPos = billboard.positionWS;
    o.positionCS = billboard.positionCS;
    if (v.hiddenX.x > 0.5)
        o.positionCS = float4(2, 2, 2, 1);
    o.color = v.color * v.spriteColor;

    o.spriteParams = half2(v.packed.x, v.packed.w);
    o.slice = v.slice;
    o.uvRect = v.uvRect;

    float4 fogClip = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
    o.fogFactor = ComputeFogFactor(fogClip.z);

    o.texcoord = v.texcoord;

    o.lighting = half4(ShadeVertexLightsSprite(anchorWS), 1.0);

    #ifndef WATER_OFF
    float scaleY = length(v.normal);
    float wp_y = anchorWS.y + posY * scaleY * 1.5;
    o.screenPos = SpriteComputeScreenPos(o.positionCS);
    o.worldPos = float4(v.hiddenX.y, wp_y, 0, 0);
    #endif

    #if BLINDEFFECT_ON
    float d = distance(worldPos, _RoBlindFocus);
    d = 1.5 - (d / _RoBlindDistance) * 1.5 + clamp((_RoBlindDistance - 50) / 120, -0.2, 0);
    o.color2.rgb = clamp(1 * d, -1, 1);
    #endif

    #else
    v.positionOS.y += _VPos;
    Billboard billboard = GetBillboard(v.positionOS, _Offset);
    worldPos = billboard.positionWS;

    o.positionCS = billboard.positionCS;
    o.color = v.color * _Color;

    float4 tempVertex = TransformObjectToHClip(v.positionOS.xyz);
    o.fogFactor = ComputeFogFactor(tempVertex.z);

    #ifdef SMOOTHPIXEL
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    float2 maskUV = (v.positionOS.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
    o.texcoord = float4(v.texcoord.x, v.texcoord.y, maskUV.x, maskUV.y);
    #else
    o.texcoord = v.texcoord;
    #endif

    float3 samplingPos = _IsMeshRenderer > 0.5 ? _LightingSamplePosition : mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    o.lighting = half4(ShadeVertexLightsSprite(samplingPos), 1.0);

    #ifndef WATER_OFF
    float3 scale = float3(
        length(unity_ObjectToWorld._m00_m10_m20),
        length(unity_ObjectToWorld._m01_m11_m21),
        length(unity_ObjectToWorld._m02_m12_m22)
    );

    float4x4 mod_ObjectToWorld = unity_ObjectToWorld;
    mod_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
    mod_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
    mod_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

    worldPos = mul(mod_ObjectToWorld, float4(v.positionOS.x, v.positionOS.y * 1.5, 0, 1)).xyz;
    o.screenPos = SpriteComputeScreenPos(o.positionCS);
    o.worldPos = float4(v.positionOS.x, worldPos.y, 0, 0);
    #endif

    #if BLINDEFFECT_ON
    float d = distance(worldPos, _RoBlindFocus);
    d = 1.5 - (d / _RoBlindDistance) * 1.5 + clamp((_RoBlindDistance - 50) / 120, -0.2, 0);
    o.color2.rgb = clamp(1 * d, -1, 1);
    #endif
    #endif

    return o;
}

half4 frag(v2f i) : SV_Target
{
    #ifdef XRAY
    clip(frac(i.positionCS.x / 2) - 0.5);
    clip(frac(i.positionCS.y / 2) - 0.5);
    #endif

    #ifdef DYNBATCH_ON
    half colorDrain_ = i.spriteParams.x;
    half width_ = i.spriteParams.y;
    #else
    half colorDrain_ = _ColorDrain;
    half width_ = _Width;
    #endif

    float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
    env = env * 0.3 + 0.7;

    #ifdef DYNBATCH_ON
    #ifdef SMOOTHPIXEL
    float2 texturePosition = i.texcoord.xy * _AtlasArraySize;
    float2 nearestBoundary = round(texturePosition);
    float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
                        abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

    float2 samplePosition = (texturePosition - nearestBoundary) / delta;
    samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

    float2 suv = clamp(samplePosition / _AtlasArraySize, i.uvRect.xy, i.uvRect.zw);
    half4 diff = SAMPLE_TEXTURE2D_ARRAY_LOD(_AtlasArray, sampler_AtlasArray, suv, i.slice, 0);
    #else
    float2 suv = clamp(i.texcoord.xy, i.uvRect.xy, i.uvRect.zw);
    half4 diff = SAMPLE_TEXTURE2D_ARRAY_LOD(_AtlasArray, sampler_AtlasArray, suv, i.slice, 0);
    #endif

    #else
    #ifdef SMOOTHPIXEL
    float2 texturePosition = i.texcoord * _MainTex_TexelSize.zw;
    float2 nearestBoundary = round(texturePosition);
    float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
                        abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

    float2 samplePosition = (texturePosition - nearestBoundary) / delta;
    samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

    half4 diff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePosition * _MainTex_TexelSize.xy);
    #else
    half4 diff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord.xy);
    #endif
    #endif

    float4 fogColor = float4(1, 1, 1, 1);

    float avg = (diff.r + diff.g + diff.b) / 3;
    diff.rgb = lerp(diff.rgb, float3(avg, avg, avg), colorDrain_);
    diff.rgb += diff * i.lighting;

    half4 c = diff * min(1.35, fogColor * i.color * float4(env.rgb, 1));
    c = saturate(c);

    clip(c.a - 0.001);

    #ifndef WATER_OFF
    float2 uv = (i.screenPos.xy / i.screenPos.w);
    float4 water = SAMPLE_TEXTURE2D(_WaterDepth, sampler_WaterDepth, uv);
    
    if (water.a >= 0.1)
    {
        float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);
        float4 waterTex = SAMPLE_TEXTURE2D(_WaterImageTexture, sampler_WaterImageTexture, wateruv);
        float height = water.z;

        waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex * 0.6);

        float simHeight = i.worldPos.y - abs(i.worldPos.x) / (width_) * 0.5;
        simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);
        waterTex *= fogColor;

        if (height > simHeight)
            c.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate((height - simHeight) * 10));
    }
    #endif

    c.rgb = MixFog(c.rgb, i.fogFactor);
    c.rgb *= c.a;

    #ifdef BLINDEFFECT_ON
    c.rgb = saturate(c.rgb * i.color2);
    #endif

    return c;
}

#endif

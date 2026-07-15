Shader "Unlit/BillboardVerticalZDepthDirect"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color("Tint", Color) = (1,1,1,1)
        [PerRendererData] _Offset("Offset", Float) = 0
        [PerRendererData] _Width("Width", Float) = 0
    }

    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        Blend One OneMinusSrcAlpha
        Cull Off
        Stencil
        {
            Ref 1
            Comp NotEqual
        }

        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ BLINDEFFECT_ON
            #pragma multi_compile_local _ WATER_OFF
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "billboard.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : TEXCOORD0;
                #if BLINDEFFECT_ON
                half4 color2 : TEXCOORD1;
                #endif
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half4 worldPos : TEXCOORD4;
                half4 envColor : TEXCOORD5;
                half width : TEXCOORD7;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

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

            UNITY_INSTANCING_BUFFER_START(SpriteProperties)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Offset)
                UNITY_DEFINE_INSTANCED_PROP(float, _Width)
            UNITY_INSTANCING_BUFFER_END(SpriteProperties)

            float4 _ClipRect;

            float4 ComputeScreenPosLocal(float4 positionCS)
            {
                float4 o = positionCS * 0.5f;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = positionCS.zw;
                return o;
            }

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o = (v2f)0;

                half offset = UNITY_ACCESS_INSTANCED_PROP(SpriteProperties, _Offset);
                half width = UNITY_ACCESS_INSTANCED_PROP(SpriteProperties, _Width);
                half4 color = UNITY_ACCESS_INSTANCED_PROP(SpriteProperties, _Color);

                Billboard billboard = GetBillboard(v.vertex, offset);
                o.pos = billboard.positionCS;
                float3 viewPos = billboard.positionVS;

                float3 planeNormal = normalize(float3(UNITY_MATRIX_V._m20, 0.0, UNITY_MATRIX_V._m22));
                float3 planePoint = UNITY_MATRIX_M._m03_m13_m23;
                float3 rayStart = _WorldSpaceCameraPos.xyz;
                float3 rayDir = -normalize(mul(UNITY_MATRIX_I_V, float4(viewPos.xyz, 1.0)).xyz - rayStart);
                float dist = rayPlaneIntersection(rayDir, rayStart, planeNormal, planePoint);
                planePoint.y += offset / 4;

                float groundDist = rayPlaneIntersection(rayDir, rayStart, float3(0, 1, 0), planePoint);
                dist = max(dist, groundDist);

                float4 planeOutPos = mul(UNITY_MATRIX_VP, float4(rayStart + rayDir * dist, 1.0));
                float newPosZ = planeOutPos.z / planeOutPos.w * o.pos.w;

                #if defined(UNITY_REVERSED_Z)
                o.pos.z = max(o.pos.z, newPosZ);
                #else
                o.pos.z = min(o.pos.z, newPosZ);
                #endif

                o.color = v.color * color;
                o.envColor = half4(saturate(SampleSH(float3(0, 1, 0))), 1);
                o.width = width;

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                o.uv = float4(v.uv.x, v.uv.y, maskUV.x, maskUV.y);

                #ifndef WATER_OFF
                float3 worldPos = mul(UNITY_MATRIX_M, float4(v.vertex.x, v.vertex.y * 1.5, 0, 1)).xyz;
                o.screenPos = ComputeScreenPosLocal(o.pos);
                o.worldPos = float4(v.vertex.x, worldPos.y, 0, 0);
                #endif

                #if BLINDEFFECT_ON
                float d = distance(planePoint, _RoBlindFocus);
                d = 1.5 - (d / _RoBlindDistance) * 1.5 + clamp((_RoBlindDistance - 50) / 120, -0.2, 0);
                o.color2.rgb = clamp(1 * d, -1, 1);
                #endif

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
                env = env * 0.5 + saturate(0.5 + i.envColor);

                float2 texturePosition = i.uv * _MainTex_TexelSize.zw;
                float2 nearestBoundary = round(texturePosition);
                float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
                    abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

                float2 samplePosition = (texturePosition - nearestBoundary) / delta;
                samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

                half4 diff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePosition * _MainTex_TexelSize.xy);

                half4 col = diff * i.color * float4(env.rgb, 1);
                col.rgb *= col.a;

                #ifndef WATER_OFF
                float2 uv = (i.screenPos.xy / i.screenPos.w);
                float4 water = SAMPLE_TEXTURE2D(_WaterDepth, sampler_WaterDepth, uv);
                float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);

                if (water.a < 0.1)
                    return col;

                float4 waterTex = SAMPLE_TEXTURE2D(_WaterImageTexture, sampler_WaterImageTexture, wateruv);
                float height = water.z;

                waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex / 2);

                float simHeight = i.worldPos.y - abs(i.worldPos.x) / i.width * 0.5;
                simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);

                if (height - 0 > simHeight)
                    col.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));
                #endif

                #ifdef BLINDEFFECT_ON
                col.rgb *= i.color2;
                #endif

                return col;
            }
            ENDHLSL
        }
    }
}

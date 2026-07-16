Shader "Unlit/BillboardVerticalZDepthPerspective"
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
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        Offset -10, -10
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "Depth"
            Tags{ "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            Blend Zero One
            Offset 0, 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "billboard.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                float3 scale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22)
                );
                v.vertex.z -= 2;

                unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
                unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
                unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

                o.uv = v.uv.xy;

                float3 vpos = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                float4 worldCoord = float4(unity_ObjectToWorld._m03_m13_m23, 1);
                float4 viewPivot = mul(UNITY_MATRIX_V, worldCoord);

                float3 forward = -normalize(viewPivot.xyz);
                float3 up = mul(UNITY_MATRIX_V, float4(0, 1, 0, 0)).xyz;
                float3 right = normalize(cross(up, forward));
                up = cross(forward, right);
                float3x3 facingRotation = float3x3(right, up, forward);

                float4 viewPos = float4(viewPivot.xyz + mul(vpos, facingRotation), 1.0);
                o.pos = mul(UNITY_MATRIX_P, viewPos);

                float3 planeNormal = normalize((_WorldSpaceCameraPos.xyz - unity_ObjectToWorld._m03_m13_m23) * float3(1, 0, 1));
                float3 planePoint = unity_ObjectToWorld._m03_m13_m23;
                float3 rayStart = _WorldSpaceCameraPos.xyz;
                float3 rayDir = -normalize(mul(UNITY_MATRIX_I_V, float4(viewPos.xyz, 1.0)).xyz - rayStart);
                float dist = rayPlaneIntersection(rayDir, rayStart, planeNormal, planePoint);

                float4 planeOutPos = mul(UNITY_MATRIX_VP, float4(rayStart + rayDir * dist, 1.0));
                float newPosZ = planeOutPos.z / planeOutPos.w * o.pos.w;

                #if defined(UNITY_REVERSED_Z)
                o.pos.z = max(o.pos.z, newPosZ);
                #else
                o.pos.z = min(o.pos.z, newPosZ);
                #endif

                o.color = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                col *= i.color;
                clip(col.a - 0.5);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Color"
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "billboard.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half4 worldPos : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            float4 _ClipRect;
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

            half _Width;
            half4 _Color;

            float4 ComputeScreenPosLocal(float4 positionCS)
            {
                float4 o = positionCS * 0.5f;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = positionCS.zw;
                return o;
            }

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                float3 scale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22)
                );

                unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
                unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
                unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

                o.uv = v.uv.xy;

                float3 vpos = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                float4 worldCoord = float4(unity_ObjectToWorld._m03_m13_m23, 1);
                float4 viewPivot = mul(UNITY_MATRIX_V, worldCoord);

                float3 forward = -normalize(viewPivot.xyz);
                float3 up = mul(UNITY_MATRIX_V, float4(0, 1, 0, 0)).xyz;
                float3 right = normalize(cross(up, forward));
                up = cross(forward, right);
                float3x3 facingRotation = float3x3(right, up, forward);

                float4 viewPos = float4(viewPivot.xyz + mul(vpos, facingRotation), 1.0);
                o.pos = mul(UNITY_MATRIX_P, viewPos);

                float3 planeNormal = normalize((_WorldSpaceCameraPos.xyz - unity_ObjectToWorld._m03_m13_m23) * float3(1, 0, 1));
                float3 planePoint = unity_ObjectToWorld._m03_m13_m23;
                float3 rayStart = _WorldSpaceCameraPos.xyz;
                float3 rayDir = -normalize(mul(UNITY_MATRIX_I_V, float4(viewPos.xyz, 1.0)).xyz - rayStart);
                float dist = rayPlaneIntersection(rayDir, rayStart, planeNormal, planePoint);

                float4 planeOutPos = mul(UNITY_MATRIX_VP, float4(rayStart + rayDir * dist, 1.0));
                float newPosZ = planeOutPos.z / planeOutPos.w * o.pos.w;

                #if defined(UNITY_REVERSED_Z)
                o.pos.z = max(o.pos.z, newPosZ);
                #else
                o.pos.z = min(o.pos.z, newPosZ);
                #endif

                o.color = v.color * _Color;

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                o.uv = float4(v.uv.x, v.uv.y, maskUV.x, maskUV.y);

                #ifndef WATER_OFF
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.x, v.vertex.y * 1.5, 0, 1)).xyz;
                o.screenPos = ComputeScreenPosLocal(o.pos);
                o.worldPos = float4(v.vertex.x, worldPos.y, 0, 0);
                #endif

                o.fogFactor = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float4 env = float4(1, 1, 1, 1);

                float2 texturePosition = i.uv * _MainTex_TexelSize.zw;
                float2 nearestBoundary = round(texturePosition);
                float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
                    abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

                float2 samplePosition = (texturePosition - nearestBoundary) / delta;
                samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

                half4 diff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePosition * _MainTex_TexelSize.xy);

                half4 col = diff * i.color * float4(env.rgb, 1);
                col.rgb = MixFog(col.rgb, i.fogFactor);
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

                float simHeight = i.worldPos.y - abs(i.worldPos.x) / (_Width) * 0.5;
                simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);

                if (height - 0 > simHeight)
                    col.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));
                #endif

                return col;
            }
            ENDHLSL
        }
    }
}

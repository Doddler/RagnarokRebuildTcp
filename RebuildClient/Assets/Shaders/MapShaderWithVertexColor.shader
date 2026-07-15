Shader "Custom/MapShaderWithVertexColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Specular ("Specular", Range(0,1)) = 0.0
        _Cutoff("Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        Blend One OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RoAdditionalLights.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 color : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Glossiness;
            half _Specular;
            half _Cutoff;
            CBUFFER_END

            float4 _RoAmbientColor;

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                o.positionWS = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                OUTPUT_LIGHTMAP_UV(v.uv2, unity_LightmapST, o.lightmapUV);
#ifndef LIGHTMAP_ON
                OUTPUT_SH(o.normalWS, o.vertexSH);
#endif
                o.fogFactor = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * i.color;
                clip(c.a - _Cutoff);

                float3 N = normalize(i.normalWS);

                half3 albedo = c.rgb * _RoAmbientColor.rgb;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = _Specular.xxx;
                surfaceData.metallic = 0;
                surfaceData.smoothness = _Glossiness;
                surfaceData.alpha = c.a;
                surfaceData.occlusion = 1;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
#if LIGHTMAP_ON
                inputData.bakedGI = SampleLightmap(i.lightmapUV, N);
#else
                inputData.bakedGI = SampleSHPixel(i.vertexSH, N);
#endif

                half4 color = RoFragmentPBRSoft(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, i.fogFactor);

                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex RoShadowVert
            #pragma fragment RoShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Glossiness;
            half _Specular;
            half _Cutoff;
            CBUFFER_END

            #include "RoShadowCaster.hlsl"
            ENDHLSL
        }
    }
}

Shader "Custom/ObjectShaderAcceptProjector"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _AmbientTex("Ambient (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Specular("Specular", Range(0,1)) = 0.0
        _Cutoff("Cutoff", Range(0,1)) = 0.5
        _AmbientIntensity("Ambient Intensity", Range(0,1)) = 1
        _LightmapIntensity("Lightmap Intensity", Range(0,1)) = 1
    }
        SubShader
        {
            Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "False" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline"}

            LOD 200

    Pass
    {
        Tags { "LightMode" = "UniversalForward" }

        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ _SHADOWS_SOFT
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoAdditionalLights.hlsl"

        struct appdata
        {
            float4 vertex : POSITION;
            float4 color    : COLOR;
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
            float3 normal : NORMAL;
        };

        struct v2f
        {
            float2 uv : TEXCOORD0;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);
            float4 color    : COLOR;
            float4 pos : SV_POSITION;
            float3 normalWS : TEXCOORD2;
            float3 positionWS : TEXCOORD3;
            half fogFactor : TEXCOORD4;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        half4 _Color;
        half _Cutoff;
        half _AmbientIntensity;
        half _LightmapIntensity;
        half _Glossiness;
        half _Specular;
        CBUFFER_END

            //from our globals
            float4 _RoAmbientColor;
            float4 _RoDiffuseColor;
            float _RoLightmapAOStrength;
            float _Opacity;

            float4x4 unity_Projector;
            float4x4 unity_ProjectorClip;

            float4 Screen(float4 a, float4 b)
            {
                return 1 - (1 - a) * (1 - b);
            }

#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
            SAMPLER(samplerunity_LightmapInd);
#endif

            half4 SampleAmbientOcclusionLightmap(float2 staticLightmapUV)
            {
#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
                return SAMPLE_TEXTURE2D(unity_LightmapInd, samplerunity_LightmapInd, staticLightmapUV);
#else
                return half4(1, 1, 1, 1);
#endif
            }

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                o.positionWS = positionWS;
                o.pos = TransformWorldToHClip(positionWS);
                o.color = v.color;
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                OUTPUT_LIGHTMAP_UV(v.uv2, unity_LightmapST, o.lightmapUV);
#ifndef LIGHTMAP_ON
                OUTPUT_SH(o.normalWS, o.vertexSH);
#endif

                o.fogFactor = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float4 diffuse = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                clip(diffuse.a - _Cutoff);

                float3 N = normalize(i.normalWS);

#if LIGHTMAP_ON
                float4 lm = float4(SampleLightmap(i.lightmapUV, N), 1) * _LightmapIntensity;
#else
                float4 lm = float4(SampleSHPixel(i.vertexSH, N), 1);
#endif
                float4 ambienttex = float4(1, 1, 1, 1);

#if LIGHTMAP_ON
                ambienttex = SampleAmbientOcclusionLightmap(i.lightmapUV);
#endif

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 L = normalize(mainLight.direction);

                float attenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float4 ambient = _RoAmbientColor;

                float shadowStr = saturate(attenuation * _Opacity + (1 - _Opacity));

                float NdotL = saturate(dot(N, L));
                float4 diffuseTerm = NdotL * shadowStr;

                lm *= (0.5 + saturate(NdotL * 2) * 0.5);

                half ambientStrength = _RoLightmapAOStrength * _AmbientIntensity;

                ambienttex = ambienttex * ambientStrength + (1 - ambientStrength);

                float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));

                float4 finalColor = saturate(NdotL * _RoDiffuseColor + clamp(_RoAmbientColor, 0, 0.5)) * shadowStr * diffuse * env + lm * 2 * (diffuse);

                finalColor *= ambienttex;

                finalColor *= 1 + (ambientStrength / 10);

#if defined(_ADDITIONAL_LIGHTS)
                uint addLightCount = GetAdditionalLightsCount();
                half3 addDiffuse = 0;
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                LIGHT_LOOP_BEGIN(addLightCount)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS);
                    half ndl = saturate(dot(N, al.direction));
                    half addAtten = RoSoftAdditionalAttenuation(lightIndex, i.positionWS);
                    addDiffuse += al.color * (addAtten * al.shadowAttenuation * ndl);
                LIGHT_LOOP_END
                finalColor.rgb += diffuse.rgb * addDiffuse;
#endif

                finalColor.rgb = MixFog(finalColor.rgb, i.fogFactor);

                return finalColor;
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
            half _Cutoff;
            half _AmbientIntensity;
            half _LightmapIntensity;
            half _Glossiness;
            half _Specular;
            CBUFFER_END

            #include "RoShadowCaster.hlsl"
            ENDHLSL
        }

        }
}

Shader "Custom/MapShaderAccurate"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Specular("Specular", Range(0,1)) = 0.0
		_Cutoff("Cutoff", Range(0,1)) = 0.5
		_LightmapFactor("Lightmap Factor", Range(0,1)) = 1
		_ShadowStrength("Shadow Strength", Range(0,1)) = 1
	}
		SubShader
		{
			Tags { "RenderType" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
			LOD 200

			Pass
			{
				Tags { "LightMode" = "UniversalForward" }

				HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_fog
				#pragma multi_compile _ LIGHTMAP_ON
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
				#pragma multi_compile _ BLINDEFFECT_ON

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
#if BLINDEFFECT_ON
					half4 color : COLOR0;
					half4 color2 : COLOR1;
#else
					half4 color : COLOR;
#endif
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
				half _LightmapFactor;
				half _ShadowStrength;
				half _Glossiness;
				half _Specular;
				CBUFFER_END

				//from our globals
				float4 _RoAmbientColor;
				float4 _RoDiffuseColor;
				float _RoLightmapAOStrength;
				float _Opacity;

#ifdef BLINDEFFECT_ON
				float4 _RoBlindFocus;
				float _RoBlindDistance;
#endif

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

					#if BLINDEFFECT_ON
						float d = distance(positionWS, _RoBlindFocus);
						d = saturate(1.5 - (d / _RoBlindDistance) * 1.5) + clamp((_RoBlindDistance-50)/120, -0.2, 0);
						o.color2.rgb = clamp(1 * d, -1, 1);
					#endif

					o.fogFactor = ComputeFogFactor(o.pos.z);
					return o;
				}

				half4 frag(v2f i) : SV_Target
				{
					float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
					clip(tex.a - _Cutoff);

					float3 N = normalize(i.normalWS);
					Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
					float3 L = normalize(mainLight.direction);
					float NdotL = saturate(dot(N, L));
					float attenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

					float4 lm = float4(0, 0, 0, 0);
					float4 ambienttex = float4(1, 1, 1, 1);

#if LIGHTMAP_ON
					lm = float4(SampleLightmap(i.lightmapUV, N), 1);
					lm = lerp(float4(0, 0, 0, 0), lm, _LightmapFactor);
#endif

					float shadowStr = 1 - (saturate(1 - attenuation * NdotL * 2) * _Opacity); //NdotL here is to force things facing away from sun to be unlit
					float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));

					NdotL = 0.5 + NdotL * 0.5;

					float4 finalColor = tex;

					finalColor *= saturate(NdotL * _RoDiffuseColor + _RoAmbientColor) * ambienttex;
					finalColor *= saturate(i.color);
					finalColor *= env; //double dipping environment color?
					finalColor *= shadowStr;
					finalColor += lm * saturate(i.color.a);

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
					finalColor.rgb += tex.rgb * addDiffuse;
#endif

					finalColor.rgb = MixFog(finalColor.rgb, i.fogFactor);
            	#ifdef BLINDEFFECT_ON
					finalColor.rgb *= i.color2.rgb;
					finalColor = saturate(finalColor);
				#endif
					finalColor.a = 1; //reject transparancy, we're opaque and using cuttoff elsewhere. Fixes minimap generation having holes.
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
				half _LightmapFactor;
				half _ShadowStrength;
				half _Glossiness;
				half _Specular;
				CBUFFER_END

				#include "RoShadowCaster.hlsl"
				ENDHLSL
			}
		}
}

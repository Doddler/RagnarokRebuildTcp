Shader "Custom/MapShaderUnlit"
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

				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

					float shadowStr = 1 - (saturate(1 - attenuation * NdotL * 2) * _Opacity); //NdotL here is to force things facing away from sun to be unlit
					float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));

					float4 s = env;
					float m = max(max(s.r, s.g), s.b);
					float world = saturate(0.5 + (s / m) * 0.5);

					NdotL = 0.5 + NdotL * 0.5;

					float4 finalColor = tex;

					finalColor *= saturate(NdotL * env + _RoAmbientColor) * ambienttex;
					finalColor *= saturate(i.color);
					finalColor *= env; //double dipping environment color?
					finalColor *= shadowStr;
					finalColor += lm * saturate(i.color.a);

					finalColor.rgb = MixFog(finalColor.rgb, i.fogFactor);
					finalColor.a = 1; //reject transparancy, we're opaque and using cuttoff elsewhere. Fixes minimap generation having holes.
					return finalColor;
				}

			ENDHLSL
			}
		}
}

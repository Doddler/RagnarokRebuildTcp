Shader "Custom/MapShaderUnlit"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Specular("Specular", Range(0,1)) = 0.0
			//        _Metallic ("Metallic", Range(0,1)) = 0.0
					_Cutoff("Cutoff", Range(0,1)) = 0.5
					_LightmapFactor("Lightmap Factor", Range(0,1)) = 1
					_ShadowStrength("Shadow Strength", Range(0,1)) = 1
	}
		SubShader
		{
			Tags { "RenderType" = "AlphaTest" "LightMode" = "ForwardBase" }
			LOD 200

			Pass
			{
				Lighting On
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				// make fog work
				#pragma multi_compile_fog
			#pragma multi_compile_fwdbase

			#pragma multi_compile _ LIGHTMAP_ON


				#pragma multi_compile _ SHADOWS_SCREEN
			#pragma multi_compile _ VERTEXLIGHT_ON



				#include "UnityCG.cginc"
			#include "AutoLight.cginc"

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
					float2 uv2 : TEXCOORD1;
					float4 color    : COLOR;
					float4 pos : SV_POSITION;
					float3 normal : TEXCOORD2;
					float3 lightDir : TEXCOORD3;

					UNITY_FOG_COORDS(4)
					LIGHTING_COORDS(5, 6)

				};

				sampler2D _MainTex;
				float4 _MainTex_ST;
				fixed4 _Color;
				fixed _Cutoff;
				fixed _LightmapFactor;
				fixed _ShadowStrength;
				
				// sampler2D unity_LightmapInd;

#ifdef DIRLIGHTMAP_COMBINED
				SamplerState samplerunity_LightmapInd;
#endif

				//unity defined variables
				uniform float4 _LightColor0;

				//from our globals
				float4 _RoAmbientColor;
				float4 _RoDiffuseColor;
				float _RoLightmapAOStrength;
				float _Opacity;



				v2f vert(appdata v)
				{
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.color = v.color;
					o.normal = normalize(v.normal).xyz;
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					o.lightDir = normalize(ObjSpaceLightDir(v.vertex));
					o.uv2 = v.uv2.xy * unity_LightmapST.xy + unity_LightmapST.zw;
					//o.worldpos = mul(unity_ObjectToWorld, v.vertex);

					
					UNITY_TRANSFER_FOG(o,o.pos);
					TRANSFER_VERTEX_TO_FRAGMENT(o);
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					float4 tex = tex2D(_MainTex, i.uv);
					clip(tex.a - _Cutoff);

					float3 L = normalize(i.lightDir);
					float3 N = normalize(i.normal);
					float NdotL = saturate(dot(N, L));
					float attenuation = LIGHT_ATTENUATION(i);

					float4 lm = float4(0, 0, 0, 0);
					float4 ambienttex = float4(1, 1, 1, 1);

					//attenuation = saturate(0.6 + attenuation * 0.4);
					//return attenuation;

					float shadowStr = 1 - (saturate(1 - attenuation * NdotL * 2) * _Opacity); //NdotL here is to force things facing away from sun to be unlit
					float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));

					float4 s = env;
					float m = max(max(s.r, s.g), s.b);
					float world = saturate(0.5 + (s / m) * 0.5);
					//return s / m;

					//return lm;

					
					//return lm;

					//lm = (lm * 2) + saturate(attenuation * 2);
					//float lmMax = max(max(lm.r,lm.g),lm.b);
					//return lm/lmMax;

					//lm = floor(lm * 32) / 32;
					//lm = floor(lm * 16) / 16;
					//return lm;

					//return env;

					NdotL = 0.5 + NdotL * 0.5;

					float4 finalColor = tex; // *0.9; //Controversial! But I think it's right.

					finalColor *= saturate(NdotL * env + _RoAmbientColor) * ambienttex;
					finalColor *= saturate(i.color);
					finalColor *= env; //double dipping environment color?
					finalColor *= shadowStr;
					finalColor += lm * saturate(i.color.a);
					
					UNITY_APPLY_FOG(i.fogCoord, finalColor);
					finalColor.a = 1; //reject transparancy, we're opaque and using cuttoff elsewhere. Fixes minimap generation having holes.
					return finalColor;


					//float4 finalColor = saturate(NdotL * _RoDiffuseColor + clamp(_RoAmbientColor,0,1)) * shadowStr * diffuse * ambienttex * i.color + lm * saturate(i.color.a);

					//float a = i.color.a;
					//i.color.a = 1;

					//UNITY_APPLY_FOG(i.fogCoord, finalColor);


				
				}

			ENDCG
			}
		}
		FallBack "Transparent/Cutout/VertexLit"
}

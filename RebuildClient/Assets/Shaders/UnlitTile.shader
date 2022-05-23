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
	}
		SubShader
		{
			Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" "BW" = "TrueProbes" "LightMode" = "ForwardBase"}
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

			//unity defined variables
			uniform float4 _LightColor0;

			//from our globals
			float4 _RoAmbientColor;

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
				// sample the texture

				float4 diffuse = tex2D(_MainTex, i.uv);

				clip(diffuse.a - _Cutoff);

				float4 lm = float4(ShadeSH9(float4(i.normal, 1)), 1);

					float3 L = normalize(i.lightDir);
					float3 N = normalize(i.normal);

					float attenuation = LIGHT_ATTENUATION(i) * 2;
					float4 ambient = _RoAmbientColor;

					float NdotL = saturate(dot(N, L));
					float4 diffuseTerm = saturate(NdotL * _LightColor0 * attenuation); //* _DiffuseTint 

					float4 finalColor = diffuse; // *(ambient + diffuseTerm);



					diffuse = diffuse * _RoAmbientColor * lerp(1,0.6,1 - diffuseTerm);
					diffuse *= i.color;

					finalColor = diffuse + lm;

					UNITY_APPLY_FOG(i.fogCoord, finalColor);

					finalColor = clamp(finalColor, 0.02, 1);

					return finalColor;



				}

			ENDCG
			}
		}
			FallBack "Transparent/Cutout/VertexLit"
}

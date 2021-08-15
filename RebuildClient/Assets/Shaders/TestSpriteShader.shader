// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/TestSpriteShader"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_Color("Tint", Color) = (1,1,1,1)
		_Offset("Offset", Float) = 0
		_Rotation("Rotation", Range(0,360)) = 0
	}

		SubShader
		{
			Tags
			{
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
				"RenderType" = "Transparent"
				"PreviewType" = "Plane"
				"CanUseSpriteAtlas" = "True"
			 "ForceNoShadowCasting" = "True"
				"DisableBatching" = "true"
			}

			Cull Off
			Lighting Off
			ZWrite Off
			Blend One OneMinusSrcAlpha


			Pass {
				ZWrite On
				ColorMask 0

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"
				#include "Billboard.cginc"

				struct v2f {
					float4 pos : SV_POSITION;
					float2 texcoord : TEXCOORD0;
				};

				v2f vert(appdata_base v)
				{
					v2f o;

					o.pos = UnityObjectToClipPos(v.vertex);

					//o.pos = Billboard2(v.vertex, 0);
					o.texcoord = v.texcoord;
					return o;
				}

				sampler2D _MainTex;

				half4 frag(v2f i) : COLOR
				{
					fixed4 c = tex2D(_MainTex, i.texcoord);
					clip(c.a - 0.5);
					return half4 (0,0,0,0);
				}
				ENDCG
			}

			Pass
			{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_fog
				#pragma multi_compile _ PIXELSNAP_ON
				#include "UnityCG.cginc"
				#include "Billboard.cginc"

				struct appdata_t
				{
					float4 vertex   : POSITION;
					float4 color    : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				struct v2f
				{
					float4 vertex   : SV_POSITION;
					fixed4 color : COLOR;
					float2 texcoord  : TEXCOORD0;
					UNITY_FOG_COORDS(2)
				};

				fixed4 _Color;
				fixed _Offset;
				fixed _Rotation;

				float4 Rotate(float4 vert)
				{
					float4 vOut = vert;
					vOut.x = vert.x * cos(radians(_Rotation)) - vert.y * sin(radians(_Rotation));
					vOut.y = vert.x * sin(radians(_Rotation)) + vert.y * cos(radians(_Rotation));
					return vOut;
				}

				v2f vert(appdata_t IN)
				{
					v2f OUT;

					IN.vertex = Rotate(IN.vertex);

					OUT.vertex = Billboard2(IN.vertex, _Offset); // _Offset);

					//float3 forward = normalize(ObjSpaceViewDir(IN.vertex));
					//IN.vertex.xyz += forward * 0.5; // _Offset;
					//OUT.vertex = UnityObjectToClipPos(IN.vertex);

					OUT.texcoord = IN.texcoord;
					OUT.color = IN.color * _Color;

					float4 tempVertex = OUT.vertex;
					OUT.vertex = UnityObjectToClipPos(IN.vertex);
					UNITY_TRANSFER_FOG(OUT, OUT.vertex);
					OUT.vertex = tempVertex;
					return OUT;
				}

				sampler2D _MainTex;

				fixed4 SampleSpriteTexture(float2 uv)
				{
					fixed4 color = tex2D(_MainTex, uv);

					return color;
				}

				fixed4 frag(v2f IN) : SV_Target
				{
					fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;


				UNITY_APPLY_FOG(IN.fogCoord, c);
				//UNITY_OPAQUE_ALPHA(c.a);

					c.rgb *= c.a;



					return c;
				}
			ENDCG
			}
		}
}
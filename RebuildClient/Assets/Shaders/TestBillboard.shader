// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/TestBillboard"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Offset("Offset", Float) = 0
	}
		SubShader
		{
			Tags {
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
				"RenderType" = "Transparent"
				"PreviewType" = "Plane"
				"CanUseSpriteAtlas" = "True"
				"ForceNoShadowCasting" = "True"
				"DisableBatching" = "true"
			}
			LOD 100

			Cull Off
			Lighting Off
			ZWrite Off
			Blend One OneMinusSrcAlpha

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				// make fog work
				#pragma multi_compile_fog

				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					UNITY_FOG_COORDS(1)
					float4 vertex : SV_POSITION;
				};

				sampler2D _MainTex;
				float4 _MainTex_ST;
				fixed _Offset;

				fixed4 BillboardPosition(float4 pos)
				{
					// billboard mesh towards camera
					float3 vpos = mul((float3x3)unity_ObjectToWorld, pos.xyz);
					float4 worldCoord = float4(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23, 1);
					float4 viewPos = mul(UNITY_MATRIX_V, worldCoord) + float4(vpos, 0);
					float4 outPos = mul(UNITY_MATRIX_P, viewPos);

					return outPos;
				}

				float DegToRad(float deg)
				{
					return deg * (3.14159 / 180.0);
				}

				float RadToDeg(float rad)
				{
					return rad * (180.0 / 3.14159);
				}

				float Angle(float3 center, float3 pos1, float3 pos2)
				{
					float3 dir1 = normalize(pos1 - center);
					float3 dir2 = normalize(pos2 - center);
					return degrees(acos(dot(dir1, dir2)));
				}

				fixed4 Billboard2(float4 pos)
				{
					float3 worldPos = mul(unity_ObjectToWorld, pos).xyz;
					float3 originPos = mul(unity_ObjectToWorld, float4(pos.x, 0, 0, 1)).xyz; //world position of origin
					float3 upPos = originPos + float3(0, 1, 0); //up from origin

					float outDist = abs(pos.y); //distance from origin should always be equal to y

					float angleA = Angle(originPos, upPos, worldPos); //angle between vertex position, origin, and up
					float angleB = Angle(worldPos, _WorldSpaceCameraPos.xyz, originPos); //angle between vertex position, camera, and origin
				
					if (pos.y > 0)
					{
						angleA = 90 - (angleA - 90);
						angleB = 90 - (angleB - 90);
					}

					float angleC = 180 - angleA - angleB; //the third angle
					
					float fixDist = 0;
					if(pos.y > 0)
						fixDist = (outDist / sin(radians(angleC))) * sin(radians(angleA)); //supposedly basic trigonometry

					float3 forward = normalize(_WorldSpaceCameraPos.xyz - worldPos);
					float3 adjust = -forward * fixDist +forward * _Offset;

					return mul(UNITY_MATRIX_VP, float4(worldPos + adjust, 1));
				}

				v2f vert(appdata v)
				{
					v2f o;

					//float4x4 mv = UNITY_MATRIX_MV;
					//float3x3 mvt = (float3x3)mv;
					//float sX = length(mvt[0]);
					//float sY = length(mvt[1]);
					//float sZ = length(mvt[2]);

					//// First column.
					//mv._m00 = sX;
					//mv._m10 = 0.0f;
					//mv._m20 = 0.0f;

					//// Second column.
					//mv._m01 = 0.0f;
					//mv._m11 = sY;
					//mv._m21 = 0.0f;

					//// Third column.
					//mv._m02 = 0.0f;
					//mv._m12 = 0.0f;
					//mv._m22 = sZ;

					//o.vertex = mul(UNITY_MATRIX_P, mul(mv, v.vertex));

					o.vertex = Billboard2(v.vertex);

					//o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					UNITY_TRANSFER_FOG(o,o.vertex);
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					// sample the texture
					fixed4 col = tex2D(_MainTex, i.uv);
				col.rgb *= col.a;
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
		}
}

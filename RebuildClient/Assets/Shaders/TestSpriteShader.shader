
Shader "Unlit/TestSpriteShader"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _Color("Tint", Color) = (1,1,1,1)
		[PerRendererData] _Offset("Offset", Float) = 0
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

				#pragma multi_compile _ WATER_BELOW WATER_ABOVE

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
			#ifdef WATER_BELOW
					clip(-1);
			#endif

					fixed4 c = tex2D(_MainTex, i.texcoord);
					clip(c.a - 0.5);
					return half4 (1,1,1,1);
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
				#pragma multi_compile _ WATER_BELOW WATER_ABOVE

				#include "UnityCG.cginc"
				#include "UnityUI.cginc"
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
					float4 screenPos : TEXCOORD1;
					half4  mask : TEXCOORD2;
					UNITY_FOG_COORDS(3)
				};

				fixed4 _Color;
				fixed _Offset;
				fixed _Rotation;


				sampler2D _MainTex;

				float4 _ClipRect;
				float4 _MainTex_ST;
				float4 _MainTex_TexelSize;
				sampler2D _WaterDepth;
								
				float _MaskSoftnessX;
				float _MaskSoftnessY;

				//from our globals
				float4 _RoAmbientColor;
				float4 _RoDiffuseColor;

				float4 Rotate(float4 vert)
				{
					float4 vOut = vert;
					vOut.x = vert.x * cos(radians(_Rotation)) - vert.y * sin(radians(_Rotation));
					vOut.y = vert.x * sin(radians(_Rotation)) + vert.y * cos(radians(_Rotation));
					return vOut;
				}

				//#define COMPUTE_DEPTH_01b -(mul( UNITY_MATRIX_MV, v.vertex ).z * _ProjectionParams.w)
				#define COMPUTE_DEPTH_01b -(view.z * _ProjectionParams.w)
				#define COMPUTE_DEPTH_01 -(UnityObjectToViewPos( v.vertex ).z * _ProjectionParams.w)

				v2f vert(appdata_t v)
				{
					v2f o;

					v.vertex = Rotate(v.vertex);

					//o.vertex = Billboard2(v.vertex, _Offset); // _Offset);

					//--------------------------------------------------------------------------------------------
					//start of billboard code
					//was taken out of the billboard.cginc because I needed access to the view matrix before 
					//projection is applied for the depth / water check
					//--------------------------------------------------------------------------------------------

					float2 pos = v.vertex.xy;


					float3 worldPos = mul(unity_ObjectToWorld, float4(pos.x, pos.y, 0, 1)).xyz;
					float3 originPos = mul(unity_ObjectToWorld, float4(pos.x, 0, 0, 1)).xyz; //world position of origin
					float3 upPos = originPos + float3(0, 1, 0); //up from origin

					float outDist = abs(pos.y); //distance from origin should always be equal to y

					float angleA = Angle(originPos, upPos, worldPos); //angle between vertex position, origin, and up
					float angleB = Angle(worldPos, _WorldSpaceCameraPos.xyz, originPos); //angle between vertex position, camera, and origin

					float camDist = distance(_WorldSpaceCameraPos.xyz, worldPos.xyz);

					if (pos.y > 0)
					{
						angleA = 90 - (angleA - 90);
						angleB = 90 - (angleB - 90);
					}

					float angleC = 180 - angleA - angleB; //the third angle

					float fixDist = 0;
					if (pos.y > 0)
						fixDist = (outDist / sin(radians(angleC))) * sin(radians(angleA)); //supposedly basic trigonometry

					//determine move as a % of the distance from the point to the camera
					float decRate = (fixDist * 0.7 - _Offset / 2) / camDist; //where does the 4 come from? Who knows!
					float decRate2 = (fixDist  ) / camDist; //where does the 4 come from? Who knows!

					float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));

					float4 pro = mul(UNITY_MATRIX_P, view);

#if UNITY_UV_STARTS_AT_TOP
					// Windows - DirectX
					view.z -= abs(UNITY_NEAR_CLIP_VALUE - view.z) * decRate2;
					pro.z -= abs(UNITY_NEAR_CLIP_VALUE - pro.z) * decRate;
#else
					// WebGL - OpenGL
					view.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate2;
					pro.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate;
#endif

					o.vertex = pro;

					//--------------------------------------------------------------------------------------------
					//end of billboard code
					//--------------------------------------------------------------------------------------------


					//float3 forward = normalize(ObjSpaceViewDir(IN.vertex));
					//IN.vertex.xyz += forward * 0.5; // _Offset;
					//OUT.vertex = UnityObjectToClipPos(IN.vertex);

					o.texcoord = v.texcoord;
					o.color = v.color * _Color;

					float4 tempVertex = UnityObjectToClipPos(v.vertex);
					UNITY_TRANSFER_FOG(o, tempVertex);


					//smoothpixelshader stuff here

					float2 pixelSize = tempVertex.w;
					pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

					float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
					float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
					o.texcoord = float4(v.texcoord.x, v.texcoord.y, maskUV.x, maskUV.y);
					o.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_MaskSoftnessX, _MaskSoftnessY) + abs(pixelSize.xy)));

					//end of smooth pixel

				

					o.screenPos = ComputeScreenPos(o.vertex);
					o.screenPos.z = COMPUTE_DEPTH_01b;

					return o;
				}


				fixed4 frag(v2f i) : SV_Target
				{
					float z = SAMPLE_DEPTH_TEXTURE_PROJ(_WaterDepth, UNITY_PROJ_COORD(i.screenPos));
					float waterDepth = Linear01Depth(z);
#ifdef WATER_BELOW
					if (waterDepth > i.screenPos.z)
						discard;
#endif
#ifdef WATER_ABOVE
					if (waterDepth <= i.screenPos.z)
						discard;
#endif


					float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));

					env = env * 0.5 + 0.5;

					//smoothpixel

					// apply anti-aliasing
					float2 texturePosition = i.texcoord * _MainTex_TexelSize.zw;
					float2 nearestBoundary = round(texturePosition);
					float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
						abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));


					float2 samplePosition = (texturePosition - nearestBoundary) / delta;
					samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

					fixed4 diff = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);

					//endsmoothpixel

					fixed4 c = diff * i.color * float4(env.rgb,1);
					

					UNITY_APPLY_FOG(i.fogCoord, c);
					//UNITY_OPAQUE_ALPHA(c.a);

					c *= i.color;

					c.rgb *= c.a;



					return c;
				}
			ENDCG
			}
		}
}
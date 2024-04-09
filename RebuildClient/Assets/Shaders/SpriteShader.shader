// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader"Ragnarok/CharacterSpriteShader"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
//		[PerRendererData] _PalTex("Palette Texture", 2D) = "white" {}
		[PerRendererData] _Color("Tint", Color) = (1,1,1,1)
		[PerRendererData] _EnvColor("Environment", Color) = (1,1,1,1)
		[PerRendererData] _Offset("Offset", Float) = 0
		[PerRendererData] _Width("Width", Float) = 0
		_Rotation("Rotation", Range(0,360)) = 0
	}

	SubShader
	{
//		Tags
//		{
//			"Queue" = "Transparent"
//			"IgnoreProjector" = "True"
//			"RenderType" = "Transparent"
//			"PreviewType" = "Plane"
//			"CanUseSpriteAtlas" = "True"
//			"ForceNoShadowCasting" = "True"
//			"DisableBatching" = "true"
//		}
		
		Tags{ "Queue" = "Transparent" "LIGHTMODE" = "Vertex" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True"  }

		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha
		
		
		Pass {
			ZWrite On
			Blend Zero One
//			AlphaToMask On


			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "Billboard.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};


			sampler2D _MainTex;
			fixed4 _Color;

			
			v2f vert(appdata_t v)
			{
				v2f o;

			//--------------------------------------------------------------------------------------------
			//start of billboard code
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
			float decRate = (fixDist * 0.7 + 0.1) / camDist; //where does the value come from? Who knows!
			float decRateNoOffset = (fixDist * 0.7) / camDist; //where does the value come from? Who knows!
			float decRate2 = (fixDist) / camDist; //where does the value come from? Who knows!


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

			o.pos = pro;

			//--------------------------------------------------------------------------------------------
			//end of billboard code
			//--------------------------------------------------------------------------------------------
	

				//o.pos = Billboard2(v.vertex, 0);
				
				o.color = v.color * _Color;
				o.texcoord = v.texcoord;
				return o;
			}

			half4 frag(v2f i) : COLOR
			{
				fixed4 c = tex2D(_MainTex, i.texcoord);
				c *= i.color;

				clip(c.a - 0.5);
				
				return c;
				return half4 (1,1,1,1);
			}
			ENDCG
		}
	
		Pass
		{
			Tags { "LIGHTMODE" = "Vertex" }
			
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile _ PIXELSNAP_ON
			#pragma multi_compile _ PALETTE_ON
			//#pragma multi_compile _ WATER_OFF
		

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"
			#include "Billboard.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord  : TEXCOORD0;
				
				float4 screenPos : TEXCOORD1;
				half4  worldPos : TEXCOORD2;
				half4  envColor : TEXCOORD3;
				UNITY_FOG_COORDS(4)
				UNITY_VERTEX_OUTPUT_STEREO

			};

			fixed4 _Color;
			fixed4 _EnvColor;
			fixed _Offset;
			fixed _Rotation;
			fixed _Width;


			sampler2D _MainTex;
			// sampler2D _PalTex;

			float4 _ClipRect;
			float4 _MainTex_ST;
			float4 _MainTex_TexelSize;
			sampler2D _WaterDepth;
			sampler2D _WaterImageTexture;
			float4 _WaterImageTexture_ST;
								
			float _MaskSoftnessX;
			float _MaskSoftnessY;

			//from our globals
			float4 _RoAmbientColor;
			float4 _RoDiffuseColor;

			float4 unity_Lightmap_ST;

			float4 Rotate(float4 vert)
			{
				float4 vOut = vert;
				vOut.x = vert.x * cos(radians(_Rotation)) - vert.y * sin(radians(_Rotation));
				vOut.y = vert.x * sin(radians(_Rotation)) + vert.y * cos(radians(_Rotation));
				return vOut;
			}

			v2f vert(appdata_t v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID()
				UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO()
				
				//v.vertex = Rotate(v.vertex);
		
				//--------------------------------------------------------------------------------------------
				//start of billboard code
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
				float decRate = (fixDist * 0.7 - _Offset/4) / camDist; //where does the value come from? Who knows!
				float decRateNoOffset = (fixDist * 0.7) / camDist; //where does the value come from? Who knows!
				float decRate2 = (fixDist) / camDist; //where does the value come from? Who knows!

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
		
				//o.texcoord = v.texcoord;
				o.color = v.color * _Color;

				//old lightprobe code
				//o.envColor = clamp(float4(ShadeSH9(fixed4(0,1,0,1)),1) * 0.5, 0, 0.35);

				//o.envColor = float4(ShadeSH9(fixed4(0,1,0,1)),1);
				o.envColor = _EnvColor; //clamp(_EnvColor * 0.5, 0, 0.35);
				
				float4 tempVertex = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(o, tempVertex);
	
				//smoothpixelshader stuff here

				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
				float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
				o.texcoord = float4(v.texcoord.x, v.texcoord.y, maskUV.x, maskUV.y);

				float4 light = float4(ShadeVertexLightsFull(v.vertex, float3(0,1,0), 8, true), 1.0);
				float lmax = max(light.r, max(light.g, light.b));
				
				light -= lmax/3;
				o.color.rgb = o.color.rgb + light.rgb;
				//o.color.a = v.color;
				
				// o.light = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				
				//
				// o.lighting.xy = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				// o.lighting.ba = v.uv1.xy * unity_Lightmap_ST.xy + unity_Lightmap_ST.zw;

				//end of smooth pixel
				#ifndef WATER_OFF

					//this mess fully removes the rotation from the matrix	
					float3 scale = float3(
						length(unity_ObjectToWorld._m00_m10_m20),
						length(unity_ObjectToWorld._m01_m11_m21),
						length(unity_ObjectToWorld._m02_m12_m22)
					);

					unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
					unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
					unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

					//build info needed for water line
					worldPos = mul(unity_ObjectToWorld, float4(pos.x, pos.y*1.5, 0, 1)).xyz; //fudge y sprite height 
					o.screenPos = ComputeScreenPos(o.vertex);
					o.worldPos = float4(pos.x, worldPos.y, 0, 0);
				#endif
								
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
				//env = env * 0.5 + 0.5;
				//float m = max(i.envColor.r, max(i.envColor.g, i.envColor.b));
				//env *= float4(i.envColor.r + m, i.envColor.g + m, i.envColor.b + m, 1);
				env = env * 0.5 + 0.5;// + saturate(0.5 + i.envColor);
				//env += i.light;
				//env = min(1.35,env);
				
				//env.rgb = (env.rgb * i.envColor.rgb) + float3(i.envColor.a, i.envColor.a, i.envColor.a) * 0.33;

				//return float4(i.light.rgb, 1);
	
				//smoothpixel
				// apply anti-aliasing
				float2 texturePosition = i.texcoord * _MainTex_TexelSize.zw;
				float2 nearestBoundary = round(texturePosition);
				float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
					abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));
	
				float2 samplePosition = (texturePosition - nearestBoundary) / delta;
				samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

				fixed4 diff = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);
				// fixed4 diff = tex2D(_MainTex,i.texcoord.xy);
				//endsmoothpixel

				// //#ifdef PALETTE_ON
				// diff *= 256;
				// diff = floor(diff);
				// diff /= 256;
				// diff = float4(tex2D(_PalTex, float2((diff.r+diff.g+diff.b)/3, 0.5)).rgb, diff.a);
				// //#endif

				// fixed4 lm = UNITY_SAMPLE_TEX2D(unity_Lightmap, i.light.xy);
				// half3 bakedColor = DecodeLightmap(lm);

				//return float4(bakedColor, 1);

				fixed4 c = diff * min(1.35, i.color * float4(env.rgb,1)); // + float4(i.light.rgb,0);
				c = saturate(c);

				if(c.a < 0.001)
					discard;
		

			
				//c *= i.color;
				c.rgb *= c.a;
	
				#ifndef WATER_OFF
					float2 uv = (i.screenPos.xy / i.screenPos.w);
					float4 water = tex2D(_WaterDepth, uv);
					float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);
				
					if (water.a < 0.1)
						return c;
	
					float4 waterTex = tex2D(_WaterImageTexture, wateruv);
					float height = water.z;
					
					waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex * 0.6);
	
					// apply fog
					UNITY_APPLY_FOG(i.fogCoord, waterTex);
	
					float simHeight = i.worldPos.y - abs(i.worldPos.x)/(_Width)*0.5;
	
					simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);
	
					if (height-0 > simHeight)
						c.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));
					//c.rgb *= waterTex.rgb;
				#else
					UNITY_APPLY_FOG(i.fogCoord, c);
				#endif

				//return float4(env.rgb, c.a);
				return c;
			}
		ENDCG
		}
	}
}

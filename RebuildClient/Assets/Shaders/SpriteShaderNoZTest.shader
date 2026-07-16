Shader"Ragnarok/CharacterSpriteShaderNoZTest"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _Color("Tint", Color) = (1,1,1,1)
		[PerRendererData] _Offset("Offset", Float) = 0
		[PerRendererData] _Width("Width", Float) = 0
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
			"RenderPipeline" = "UniversalPipeline"
		}

		Cull Off
		ZWrite Off
		ZTest Off
		Blend One OneMinusSrcAlpha

		Pass
		{
			Tags{ "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile _ SMOOTHPIXEL

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "billboard.cginc"

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				half4 color : TEXCOORD0;
				float2 texcoord : TEXCOORD1;
				float4 screenPos : TEXCOORD2;
				half4 worldPos : TEXCOORD3;
				half fogFactor : TEXCOORD4;
			};

			half4 _Color;
			half _Offset;
			half _Rotation;
			half _Width;

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			float4 _MainTex_ST;
			float4 _MainTex_TexelSize;
			TEXTURE2D(_WaterDepth);
			SAMPLER(sampler_WaterDepth);
			TEXTURE2D(_WaterImageTexture);
			SAMPLER(sampler_WaterImageTexture);
			float4 _WaterImageTexture_ST;

			float4 _RoAmbientColor;
			float4 _RoDiffuseColor;

			float4 ComputeScreenPosLocal(float4 positionCS)
			{
				float4 o = positionCS * 0.5f;
				o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
				o.zw = positionCS.zw;
				return o;
			}

			v2f vert(appdata_t v)
			{
				v2f o = (v2f)0;

				float2 pos = v.vertex.xy;

				float3 worldPos = mul(unity_ObjectToWorld, float4(pos.x, pos.y, 0, 1)).xyz;
				float3 originPos = mul(unity_ObjectToWorld, float4(pos.x, 0, 0, 1)).xyz;
				float3 upPos = originPos + float3(0, 1, 0);

				float outDist = abs(pos.y);

				float angleA = Angle(originPos, upPos, worldPos);
				float angleB = Angle(worldPos, _WorldSpaceCameraPos.xyz, originPos);

				float camDist = distance(_WorldSpaceCameraPos.xyz, worldPos.xyz);

				if (pos.y > 0)
				{
					angleA = 90 - (angleA - 90);
					angleB = 90 - (angleB - 90);
				}

				float angleC = 180 - angleA - angleB;

				float fixDist = 0;
				if (pos.y > 0)
					fixDist = (outDist / sin(radians(angleC))) * sin(radians(angleA));

				float decRate = (fixDist * 0.7 - _Offset / 4) / camDist;
				float decRate2 = (fixDist) / camDist;

				float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
				float4 pro = mul(UNITY_MATRIX_P, view);

				#if UNITY_UV_STARTS_AT_TOP
				view.z -= abs(UNITY_NEAR_CLIP_VALUE - view.z) * decRate2;
				pro.z -= abs(UNITY_NEAR_CLIP_VALUE - pro.z) * decRate;
				#else
				view.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate2;
				pro.z += abs(UNITY_NEAR_CLIP_VALUE) * decRate;
				#endif

				o.vertex = pro;

				o.texcoord = v.texcoord;
				o.color = v.color * _Color;

				o.fogFactor = ComputeFogFactor(TransformObjectToHClip(v.vertex.xyz).z);

				#ifndef WATER_OFF
				float3 scale = float3(
					length(unity_ObjectToWorld._m00_m10_m20),
					length(unity_ObjectToWorld._m01_m11_m21),
					length(unity_ObjectToWorld._m02_m12_m22)
				);

				unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
				unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
				unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

				worldPos = mul(unity_ObjectToWorld, float4(pos.x, pos.y * 1.5, 0, 1)).xyz;
				o.screenPos = ComputeScreenPosLocal(o.vertex);
				o.worldPos = float4(pos.x, worldPos.y, 0, 0);
				#endif

				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
				env = env * 0.5 + 0.5;

				#ifdef SMOOTHPIXEL
				float2 texturePosition = i.texcoord * _MainTex_TexelSize.zw;
				float2 nearestBoundary = round(texturePosition);
				float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
					abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

				float2 samplePosition = (texturePosition - nearestBoundary) / delta;
				samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

				half4 diff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePosition * _MainTex_TexelSize.xy);
				#else
				half4 diff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord.xy);
				#endif

				half4 c = diff * i.color * float4(env.rgb, 1);

				if (c.a < 0.001)
					discard;

				c.rgb = MixFog(c.rgb, i.fogFactor);

				c *= i.color;
				c.rgb *= c.a;

				#ifndef WATER_OFF
				float2 uv = (i.screenPos.xy / i.screenPos.w);
				float4 water = SAMPLE_TEXTURE2D(_WaterDepth, sampler_WaterDepth, uv);
				float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);

				if (water.a < 0.1)
					return c;

				float4 waterTex = SAMPLE_TEXTURE2D(_WaterImageTexture, sampler_WaterImageTexture, wateruv);
				float height = water.z;

				waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex / 2);

				float simHeight = i.worldPos.y - abs(i.worldPos.x) / (_Width) * 0.5;
				simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);

				if (height - 0 > simHeight)
					c.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));
				#endif

				return c;
			}
			ENDHLSL
		}
	}
}

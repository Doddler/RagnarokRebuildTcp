// borrowed graciously under GPL from https://github.com/Reinisch/Darkest-Dungeon-Unity/blob/master/Assets/Shaders/UI-Grayscale.shader

Shader "Custom/UI/Grayscale"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_EffectAmount ("Effect Amount", Range (0, 1)) = 1.0
		_BrightnessAmount ("Brightness Amount", Range(0.0, 3)) = 1.0

		_ColorMask ("Color Mask", Float) = 15
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
			"RenderPipeline"="UniversalPipeline"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
			Tags{ "LightMode" = "UniversalForward" }

		HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				half4 color    : COLOR;
				half2 texcoord  : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
			};

			half4 _Color;
			half4 _TextureSampleAdd;

			bool _UseClipRect;
			float4 _ClipRect;

			bool _UseAlphaClip;
			uniform float _EffectAmount;
			uniform float _BrightnessAmount;

			float UiGet2DClipping(float2 position, float4 clipRect)
			{
				float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
				return inside.x * inside.y;
			}

			v2f vert(appdata_t IN)
			{
				v2f OUT = (v2f)0;
				OUT.worldPosition = IN.vertex;
				OUT.vertex = TransformObjectToHClip(OUT.worldPosition.xyz);

				OUT.texcoord = IN.texcoord;

				OUT.color = IN.color * _Color;
				return OUT;
			}

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			half4 frag(v2f IN) : SV_Target
			{
				half4 color = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

				if (_UseClipRect)
					color *= UiGet2DClipping(IN.worldPosition.xy, _ClipRect);

				if (_UseAlphaClip)
					clip (color.a - 0.001);

				float3 brtColor = color.rgb * _BrightnessAmount;
				color.rgb = lerp(brtColor, dot(brtColor, float3(0.3, 0.59, 0.11)), _EffectAmount);
				return color;
			}
		ENDHLSL
		}
	}
	FallBack "UI/Default"
}

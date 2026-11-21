// Lights a UI bar by height: a proportional highlight, a shadow towards the bottom and a thin glint near the top.
// Height comes from the sprite's uv0.y, so the sprite must be its own texture rather than in an atlas.
// Slicing at the top and bottom makes the height uneven at those edges, which shifts the lighting slightly.

Shader "Custom/UI/LitBar"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.35
		_HighlightPos ("Highlight Position", Range(0, 1)) = 0.15
		_HighlightWidth ("Highlight Width", Range(0.01, 1)) = 0.2

		_ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.25
		_ShadowStart ("Shadow Start", Range(0, 1)) = 0.6

		_GlintStrength ("Glint Strength", Range(0, 1)) = 0.25
		_GlintPos ("Glint Position", Range(0, 1)) = 0.05
		_GlintWidth ("Glint Width", Range(0.005, 0.5)) = 0.03

		_Bands ("Bands (0 = smooth)", Range(0, 32)) = 8

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

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
				float height : TEXCOORD2;
			};

			half4 _Color;
			half4 _TextureSampleAdd;

			float _HighlightStrength;
			float _HighlightPos;
			float _HighlightWidth;
			float _ShadowStrength;
			float _ShadowStart;
			float _GlintStrength;
			float _GlintPos;
			float _GlintWidth;
			float _Bands;

			bool _UseClipRect;
			float4 _ClipRect;

			float UiGet2DClipping(float2 position, float4 clipRect)
			{
				float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
				return inside.x * inside.y;
			}

			float Gaussian(float x, float center, float width)
			{
				float d = (x - center) / width;
				return exp(-d * d);
			}

			v2f vert(appdata_t IN)
			{
				v2f OUT = (v2f)0;
				OUT.worldPosition = IN.vertex;
				OUT.vertex = TransformObjectToHClip(OUT.worldPosition.xyz);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;
				OUT.height = IN.texcoord.y;
				return OUT;
			}

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			half4 frag(v2f IN) : SV_Target
			{
				half4 color = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

				if (_UseClipRect)
					color.a *= UiGet2DClipping(IN.worldPosition.xy, _ClipRect);

				float h = saturate(IN.height);
				if (_Bands >= 1)
					h = (floor(min(h, 0.9999) * _Bands) + 0.5) / _Bands;
				float t = 1 - h;

				float3 lit = color.rgb * (1 + _HighlightStrength * Gaussian(t, _HighlightPos, _HighlightWidth));

				//a channel past 1 is folded back and spent on moving towards white, so saturated colours still brighten
				float peak = max(lit.r, max(lit.g, lit.b));
				if (peak > 1)
					lit = lerp(lit / peak, 1, saturate(peak - 1));

				lit *= 1 - _ShadowStrength * smoothstep(_ShadowStart, 1, t);
				lit = lerp(lit, 1, _GlintStrength * Gaussian(t, _GlintPos, _GlintWidth));

				color.rgb = lit;
				return color;
			}
		ENDHLSL
		}
	}
	FallBack "UI/Default"
}

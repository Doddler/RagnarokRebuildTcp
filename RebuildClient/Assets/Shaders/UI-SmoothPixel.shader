Shader "Custom/UI/UiSmoothPixel"
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
            #define SMOOTH_PIXEL 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float4 texcoord : TEXCOORD0;
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

                OUT.texcoord = float4(IN.texcoord, 0, 0);

                //smoothpixelshader stuff here
                #ifdef SMOOTH_PIXEL
				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
				float2 maskUV = (IN.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
				OUT.texcoord = float4(IN.texcoord.x, IN.texcoord.y, maskUV.x, maskUV.y);
                #endif
                //end of smooth pixel

                OUT.color = IN.color * _Color;
                return OUT;
            }

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            half4 frag(v2f IN) : SV_Target
            {
                #ifdef SMOOTH_PIXEL
				//smoothpixel
				// apply anti-aliasing
				float2 texturePosition = IN.texcoord.xy * _MainTex_TexelSize.zw;
				float2 nearestBoundary = round(texturePosition);
				float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
					abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

				float2 samplePosition = (texturePosition - nearestBoundary) / delta;
				samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

				half4 color = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePosition * _MainTex_TexelSize.xy) + _TextureSampleAdd) * IN.color;
				//endsmoothpixel
                #else
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord.xy) * IN.color;
                #endif

                if (_UseClipRect)
                    color *= UiGet2DClipping(IN.worldPosition.xy, _ClipRect);

                if (_UseAlphaClip)
                    clip(color.a - 0.001);

                return color;
            }
            ENDHLSL
        }
    }
    FallBack "UI/Default"
}

Shader "Ragnarok/EffectShaderSmoothsprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("BlendSource", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("BlendDestination", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "ForceNoShadowCasting" = "True"
            "DisableBatching" = "true"
        }
        LOD 100

        Cull [_Cull]
        Lighting Off
        ZWrite [_ZWrite]
        ZTest[_myCustomCompare]
        Blend [_SrcBlend] [_DstBlend]


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma shader_feature MULTIPLY_ALPHA

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //UNITY_FOG_COORDS(1)
                float4 color    : COLOR;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
			float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                //smoothpixelshader stuff here

				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
				float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
				o.uv = float4(v.uv.x, v.uv.y, maskUV.x, maskUV.y);

				//end of smooth pixel
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
				//smoothpixel
				// apply anti-aliasing
				float2 texturePosition = i.uv * _MainTex_TexelSize.zw;
				float2 nearestBoundary = round(texturePosition);
				float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
					abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));
	
				float2 samplePosition = (texturePosition - nearestBoundary) / delta;
				samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

				fixed4 diff = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);
				//endsmoothpixel

                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                fixed4 c = diff * i.color * float4(_Color.rgb,1);
                c.rgb *= c.a;
                return c;
#if MULTIPLY_ALPHA
                return col * c * c.a;
#else
                return col * c;
#endif
            }
            ENDCG
        }
    }
}

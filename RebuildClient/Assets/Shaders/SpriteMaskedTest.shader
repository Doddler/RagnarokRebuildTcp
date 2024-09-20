Shader "Unlit/SpriteMaskedTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Color2("Color1", Color) = (1,1,1,1)
        _Color3("Color2", Color) = (1,1,1,1)
    }
    SubShader
    {
		
		Tags{ "Queue" = "Transparent" "LIGHTMODE" = "Vertex" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True"  }

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
            float4 _MainTex_TexelSize;
            sampler2D _MaskTex;
            float4 _MainTex_ST;
            float4 _ClipRect;
            fixed4 _Color;
            fixed4 _Color2;
            fixed4 _Color3;

            float3 RGBToHSV(float3 c)
	        {
	            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	            float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
	            float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
	            float d = q.x - min( q.w, q.y );
	            float e = 1.0e-10;
	            return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
	        }

	        float3 HSVToRGB( float3 c )
	        {
	            float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
	            float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
	            return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
	        }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                //smoothpixelshader stuff here

				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
				float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
				o.uv = float4(v.uv.x, v.uv.y, maskUV.x, maskUV.y);

				//end of smooth pixel
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                //fixed4 col = tex2D(_MainTex, i.uv);
                //fixed4 m = tex2D(_MaskTex, i.uv);
                // apply fog

                //smoothpixel
				// apply anti-aliasing
				float2 texturePosition = i.uv * _MainTex_TexelSize.zw;
				float2 nearestBoundary = round(texturePosition);
				float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
					abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));
	
				float2 samplePosition = (texturePosition - nearestBoundary) / delta;
				samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

				fixed4 col = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);
				fixed4 m = tex2D(_MaskTex, samplePosition * _MainTex_TexelSize.xy);
				//endsmoothpixel

            	//fixed4 col = tex2D(_MainTex, i.uv);
                //fixed4 m = tex2D(_MaskTex, i.uv);

            	float3 hsv = RGBToHSV(_Color2);

            	float factor = _Color2.a;
            	factor = factor - (1-m.r) * (2 * factor);
            	if(factor > 0)
            	{
            		float num = hsv.z + factor - 1;
            		hsv.z += factor;
            		if(num > 0)
            			hsv.y -= num;
            	}
            	else
            	{
            		factor *= -1;
            		float num = hsv.z - factor;
            		hsv.z -= factor;
            		if(num < 0)
            			hsv.y -= num;
            	}
            	
				float3 rgb = HSVToRGB(hsv);
            	
                col.rgb = lerp(col.rgb, rgb, m.a);
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col * col.a;
            }
            ENDCG
        }
    }
}

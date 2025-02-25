﻿Shader "Custom/IllusionStatusAlt"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength("Strength", Range(0,1)) = 0
    }
    SubShader
    {
	    //** No culling or depth -- use for post proc
        Cull Off ZWrite Off ZTest Always
        //** No culling -- use for non-transparent sprites.
        //Cull Off ZWrite On ZTest Always
        //** transparent sprite
        //Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        //Zwrite On 
        //Blend SrcAlpha OneMinusSrcAlpha
        //** 3D things
        //Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        //Zwrite On 
        // No culling or depth -- use for post proc.

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            //#pragma fragment sampling
            //#pragma fragment swizzle
			//#pragma fragment inversion
            #pragma fragment distort
            //#pragma fragment ChromaTint

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Strength;

            //stolen from shadergraph
            inline float unity_noise_randomValue (float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453);
            }

            inline float unity_noise_interpolate (float a, float b, float t)
            {
                return (1.0-t)*a + (t*b);
            }

            inline float unity_valueNoise (float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                uv = abs(frac(uv) - 0.5);
                float2 c0 = i + float2(0.0, 0.0);
                float2 c1 = i + float2(1.0, 0.0);
                float2 c2 = i + float2(0.0, 1.0);
                float2 c3 = i + float2(1.0, 1.0);
                float r0 = unity_noise_randomValue(c0);
                float r1 = unity_noise_randomValue(c1);
                float r2 = unity_noise_randomValue(c2);
                float r3 = unity_noise_randomValue(c3);

                float bottomOfGrid = unity_noise_interpolate(r0, r1, f.x);
                float topOfGrid = unity_noise_interpolate(r2, r3, f.x);
                float t = unity_noise_interpolate(bottomOfGrid, topOfGrid, f.y);
                return t;
            }

            void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
            {
                float t = 0.0;

                float freq = pow(2.0, float(0));
                float amp = pow(0.5, float(3-0));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(1));
                amp = pow(0.5, float(3-1));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(2));
                amp = pow(0.5, float(3-2));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                Out = t;
            }
            

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 distort (v2f iTexCoord ) : SV_Target
            {
                float2 texCoord = iTexCoord.uv;
                
                float noise;
                float noise2;
                float noise3;

                Unity_SimpleNoise_float(texCoord + _SinTime.z, 20, noise);
                Unity_SimpleNoise_float(texCoord + _SinTime.y, 20, noise2);
                Unity_SimpleNoise_float(texCoord + _SinTime.y * 3.14, 5, noise3);

                texCoord = lerp(texCoord, float2(noise, noise2) + noise3, _Strength * 0.035);
                
                fixed4 col = tex2D(_MainTex, texCoord);
                return col;
            }
            
            ENDCG
        }
    }
}
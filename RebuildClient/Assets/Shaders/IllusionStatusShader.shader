﻿Shader "Custom/IllusionStatus"
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
            #pragma fragment swirl
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 sampling (v2f iTexCoord ) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, iTexCoord.uv);
                return col;
            }
            
            fixed4 swizzle (v2f iTexCoord ) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, iTexCoord.uv).gbra;
                return col;
            }
            
            fixed4 inversion (v2f iTexCoord) : SV_Target
            { 
                fixed4 texColor = tex2D(_MainTex, iTexCoord.uv);
                texColor.rgb = 1 - texColor.rgb;
                return texColor;
            }
            
            fixed4 ChromaTint (v2f iTexCoord) : SV_Target
            {
                float4 texColor = tex2D(_MainTex, iTexCoord.uv);
                
                //check for our chromakey
                if( texColor.g < texColor.b - 0.1 && 
                    texColor.g < texColor.r - 0.1)
                {
                    float delta = abs(texColor.b - texColor.r);
                    if(delta < 0.2)
                    {
                        return float4(
                            texColor.r * 2, 
                            texColor.g * 0.5, 
                            texColor.b * 2, 
                            texColor.a);
                    }
                }
                
                //convert to grayscale 
                float gray = texColor.r * 0.3 + 
                             texColor.g * 0.5 + 
                             texColor.b * 0.11;

                return float4(gray, gray * 0.9, gray * 0.75, texColor.a);
            }
           
            
            fixed4 timeShift (v2f iTexCoord) : SV_Target
            {
                float2 texCoord = iTexCoord.uv;
                texCoord.x +=_SinTime.x;
                float4 texColor = tex2D(_MainTex, texCoord);

                return texColor;
            }
            
            fixed4 swirl (v2f iTexCoord ) : SV_Target
            {
                float2 texCoord = iTexCoord.uv;
                texCoord -= float2(0.5,0.5);
                
                float radius = length(texCoord);
                float angle = 0;
                if(texCoord.x != 0)
                    angle = atan(texCoord.y / texCoord.x);
                if(texCoord.x < 0)
                    angle += 3.141592;
                
                angle += _SinTime.y * radius / 8;
                
                texCoord.x = radius * cos(angle);
                texCoord.y = radius * sin(angle);
                
                texCoord.x = texCoord.x + sin(texCoord.y * 15 + _Time.z) * 0.005;
                //texCoord.y *= 0.8 + sin(_Time.x + texCoord.x);// _SinTime.x / 5;
                                
                texCoord += float2(0.5,0.5);
                texCoord = lerp(texCoord, iTexCoord.uv, 1 - _Strength);
                fixed4 col = tex2D(_MainTex, texCoord);
                return col;
            }
            
            ENDCG
        }
    }
}
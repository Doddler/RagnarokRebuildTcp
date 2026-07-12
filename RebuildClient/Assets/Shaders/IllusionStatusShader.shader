Shader "Custom/IllusionStatus"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength("Strength", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment swirl

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Strength;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 sampling (v2f iTexCoord ) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, iTexCoord.uv);
                return col;
            }

            half4 swizzle (v2f iTexCoord ) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, iTexCoord.uv).gbra;
                return col;
            }

            half4 inversion (v2f iTexCoord) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, iTexCoord.uv);
                texColor.rgb = 1 - texColor.rgb;
                return texColor;
            }

            half4 ChromaTint (v2f iTexCoord) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, iTexCoord.uv);

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

                float gray = texColor.r * 0.3 +
                             texColor.g * 0.5 +
                             texColor.b * 0.11;

                return float4(gray, gray * 0.9, gray * 0.75, texColor.a);
            }


            half4 timeShift (v2f iTexCoord) : SV_Target
            {
                float2 texCoord = iTexCoord.uv;
                texCoord.x +=_SinTime.x;
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord);

                return texColor;
            }

            half4 swirl (v2f iTexCoord ) : SV_Target
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

                texCoord += float2(0.5,0.5);
                texCoord = lerp(texCoord, iTexCoord.uv, 1 - _Strength);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texCoord);
                return col;
            }

            ENDHLSL
        }
    }
}

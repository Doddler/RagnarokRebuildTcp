Shader "Unlit/GroundBlendTest"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Lightmap("Lightmap", 2D) = "white" {}
        _Ambient("Ambient", Color) = (1,1,1,1)
        _Diffuse("Diffuse", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Lightmap);
            SAMPLER(sampler_Lightmap);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Lightmap_ST;
            half4 _Ambient;
            half4 _Diffuse;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(o.vertex.z);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 lm = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, i.uv/30);

                return lm;

                half4 light = saturate(_Ambient + _Diffuse)/2 * lm.a;

                return col * light + lm;

                return col*_Ambient*lm.a+lm;
            }
            ENDHLSL
        }
    }
}

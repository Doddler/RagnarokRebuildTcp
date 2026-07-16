Shader "Ragnarok/AlphaBlendParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TintColor ("Legacy Tint", Color) = (0.5,0.5,0.5,0.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
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
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _TintColor;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(o.vertex.z);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = 2.0h * i.color * _Color * _TintColor * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                col.a = saturate(col.a);
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}

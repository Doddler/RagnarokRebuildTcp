Shader "Projector/Multiply"
{
    Properties
    {
        _ShadowTex("Cookie", 2D) = "gray" {}
        _FalloffTex("FallOff", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ColorMask RGB
            Blend SrcAlpha One
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct v2f
            {
                float4 uvShadow : TEXCOORD0;
                float4 uvFalloff : TEXCOORD1;
                float4 pos : SV_POSITION;
            };

            float4x4 unity_Projector;
            float4x4 unity_ProjectorClip;

            v2f vert(float4 vertex : POSITION)
            {
                v2f o = (v2f)0;
                o.pos = TransformObjectToHClip(vertex.xyz);
                o.uvShadow = mul(unity_Projector, vertex);
                o.uvFalloff = mul(unity_ProjectorClip, vertex);
                return o;
            }

            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);
            TEXTURE2D(_FalloffTex);
            SAMPLER(sampler_FalloffTex);

            half4 frag(v2f i) : SV_Target
            {
                half4 texS = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, i.uvShadow.xy / i.uvShadow.w);
                half4 texF = SAMPLE_TEXTURE2D(_FalloffTex, sampler_FalloffTex, i.uvFalloff.xy / i.uvFalloff.w);
                half4 res = lerp(half4(1, 1, 1, 0), texS, texF.a);
                return res;
            }
            ENDHLSL
        }
    }
}

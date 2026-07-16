// NOTE: Legacy Projector component is unsupported under URP. This shader compiles
Shader "Projector/AdditiveTint"
{
    Properties
    {
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Attenuation ("Falloff", Range(0.0, 1.0)) = 1.0
        _ShadowTex ("Cookie", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }
            ZWrite Off
            ColorMask RGB
            Blend SrcAlpha One
            Offset 0, 0
            ZTest Equal

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct v2f
            {
                float4 uvShadow : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            float4x4 unity_Projector;
            float4x4 unity_ProjectorClip;

            v2f vert (float4 vertex : POSITION)
            {
                v2f o = (v2f)0;
                o.pos = TransformObjectToHClip(vertex.xyz);
                o.uvShadow = mul(unity_Projector, vertex);
                return o;
            }

            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);
            half4 _Color;
            float _Attenuation;

            half4 frag (v2f i) : SV_Target
            {
                float4 texCookie = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, i.uvShadow.xy / i.uvShadow.w);
                float4 outColor = 1 - (1 - _Color) * (1 - texCookie);
                outColor.a = _Color.a * texCookie.a;

                float depth = i.uvShadow.z;
                return outColor * clamp(1.0 - abs(depth) + _Attenuation, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}

Shader "Unlit/AlphaPerspectiveShader"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _InvFade ("Soft Particles Factor", Range(0.01,5.0)) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off ZWrite Off

        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _TintColor;
            float _InvFade;

            struct appdata_t
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : TEXCOORD0;
                float3 texcoord : TEXCOORD1;
                float4 projPos : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            v2f vert(appdata_t v)
            {
                v2f o = (v2f)0;
                VertexPositionInputs vpi = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = vpi.positionCS;
                o.projPos = vpi.positionNDC;
                o.projPos.z = -vpi.positionVS.z;
                o.color = v.color * _TintColor;
                o.texcoord = v.texcoord;
                o.texcoord.xy = o.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                o.fogFactor = ComputeFogFactor(o.vertex.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float sceneZ = LinearEyeDepth(SampleSceneDepth(i.projPos.xy / i.projPos.w), _ZBufferParams);
                float partZ = i.projPos.z;
                float fade = saturate(_InvFade * (sceneZ - partZ));
                i.color.a *= fade;

                float2 uv = float2(i.texcoord.x / i.texcoord.z, i.texcoord.y);
                half4 col = 2.0f * i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                col.a = saturate(col.a);

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}

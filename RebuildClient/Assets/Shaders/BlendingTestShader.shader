Shader "Unlit/BlendingTestShader"
{
    Properties
    {

    }
    SubShader
    {
    Tags
            {
                "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
            }

            Cull Off
            ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 color    : COLOR;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color;

                // compute depth
                o.screenPos = ComputeScreenPos(o.vertex);
                o.screenPos.z = -TransformWorldToView(TransformObjectToWorld(v.vertex.xyz)).z;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float depth = SampleSceneDepth(screenUV);
                depth = Linear01Depth(depth, _ZBufferParams);

                clip(0.01 - depth);

                return i.color;
            }
            ENDHLSL
        }
    }
}

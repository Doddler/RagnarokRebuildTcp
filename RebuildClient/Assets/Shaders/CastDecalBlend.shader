Shader "Custom/CastDecalBlend"
{
    Properties
    {
        _MainTex ("Cookie", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Attenuation ("Falloff", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DecalScreenSpaceProjector"
            Tags { "LightMode" = "DecalScreenSpaceProjector" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZTest Greater
            ZWrite Off

            HLSLPROGRAM
            #pragma target 2.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Attenuation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;

            #if UNITY_REVERSED_Z
                float depth = SampleSceneDepth(screenUV);
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
            #endif

                float3 positionWS = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);

                float3 positionDS = TransformWorldToObject(positionWS);
                positionDS *= float3(1.0, -1.0, 1.0);
                clip(0.5 - Max3(abs(positionDS).x, abs(positionDS).y, abs(positionDS).z));

                float2 decalUV = positionDS.xz + 0.5;
                float2 uv = TRANSFORM_TEX(decalUV, _MainTex);
                half4 cookie = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 col = _Color * cookie;
                col *= saturate(1.0 - abs(positionDS.y * 2.0) + _Attenuation);
                return col;
            }
            ENDHLSL
        }
    }
}

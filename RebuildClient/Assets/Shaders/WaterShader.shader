Shader "Ragnarok/RoWaterShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WaveHeight("Height", Float) = 0
        _WaveSpeed("Speed", Float) = 0
        _WavePitch("Pitch", Float) = 0
        _Color("Tint", Color) = (1,1,1,0.5)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-2"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "WaterMode" = "On"
            "PreviewType" = "Plane"
            "ForceNoShadowCasting" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Stencil
        {
            Ref 1
            Comp NotEqual
        }

        Pass
        {
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ BLINDEFFECT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
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
            float _WaveHeight;
            float _WavePitch;
            float _WaveSpeed;
            float4 _Color;
            CBUFFER_END

            float4 _RoDiffuseColor;
            float4 _RoAmbientColor;

            #ifdef BLINDEFFECT_ON
            float4 _RoBlindFocus;
            float _RoBlindDistance;
            #endif

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                float offset = (_Time.x * 1000 * _WaveSpeed) % 360 - 180;
                float x = worldPos.x % 2.0;
                float y = worldPos.z % 2.0;

                float diff = x < 1.0 ? y < 1.0 ? 1.0 : -1.0 : 0.0;

                worldPos.y += sin((3.1415926 / 180) * (offset + 0.5 * _WavePitch * (worldPos.x + worldPos.z + diff))) *
                    _WaveHeight;

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(TransformObjectToHClip(v.vertex.xyz).z);

                float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
                o.vertex = mul(UNITY_MATRIX_P, view);

                #if BLINDEFFECT_ON
                float3 pos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(pos, _RoBlindFocus);
                d = 1.2 - d / _RoBlindDistance;
                o.color.rgb = 1 * clamp(1 * d, -1, 1);
                #else
                o.color.rgb = float3(1, 1, 1);
                #endif

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                half a = 0.5625;
                float env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
                env = env * 0.5 + 0.5;

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return half4(col.rgb * i.color.rgb * env * a, a);
            }
            ENDHLSL
        }
    }
}

Shader "Unlit/SceneHeightShader"
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
        Tags {
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
            "WaterMode" = "On"
            "PreviewType" = "Plane"
            "ForceNoShadowCasting" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
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

            float _RoWaterWaveHeight;
            float _RoWaterWavePitch;
            float _RoWaterWaveSpeed;

            v2f vert(appdata v)
            {
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float4 vin = v.vertex;

                float offset = (_Time.x * 1000 * _RoWaterWaveSpeed) % 360 - 180;
                float x = worldPos.x % 2.0;
                float y = worldPos.z % 2.0;

                float diff = x < 1.0 ? y < 1.0 ? 1.0 : -1.0 : 0.0;

                float pos = worldPos.y + sin((3.1415926 / 180) * (offset + 0.5 * _RoWaterWavePitch * (worldPos.x + worldPos.z + diff))) * _RoWaterWaveHeight;

                worldPos.y += sin((3.1415926 / 180) * (offset + 0.5 * _RoWaterWavePitch * (worldPos.x + worldPos.z + diff))) * _RoWaterWaveHeight;

                v.vertex = mul(unity_WorldToObject, worldPos);

                v2f o = (v2f)0;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
                o.vertex = mul(UNITY_MATRIX_P, view);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.screenPos = float4(v.uv.x % 1, v.uv.y % 1, pos, 1);

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(i.uv.x%1, i.uv.y%1, i.screenPos.z, 1);
            }
            ENDHLSL
        }
    }
}

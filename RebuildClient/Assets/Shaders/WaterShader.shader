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
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Stencil
        {
            Ref 1
            Comp NotEqual
        }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile _ BLINDEFFECT_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 color    : COLOR;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            //sampler2D _CameraDepthTexture;
            float4 _MainTex_ST;
            float _WaveHeight;
            float _WavePitch;
            float _WaveSpeed;
            float4 _Color;

            //from our globals
            float4 _RoDiffuseColor;
            float4 _RoAmbientColor;

            #ifdef BLINDEFFECT_ON
				float4 _RoBlindFocus;
				float _RoBlindDistance;
            #endif

            v2f vert(appdata v)
            {
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                float offset = (_Time.x * 1000 * _WaveSpeed) % 360 - 180;
                float x = worldPos.x % 2.0;
                float y = worldPos.z % 2.0;

                float diff = x < 1.0 ? y < 1.0 ? 1.0 : -1.0 : 0.0;

                worldPos.y += sin((3.1415926 / 180) * (offset + 0.5 * _WavePitch * (worldPos.x + worldPos.z + diff))) *
                    _WaveHeight;

                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, UnityObjectToClipPos(v.vertex));

                float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
                o.vertex = mul(UNITY_MATRIX_P, view);

				#if BLINDEFFECT_ON
					float3 pos = mul(unity_ObjectToWorld, v.vertex);
					float d = distance(pos, _RoBlindFocus);
					d = 1.2 - d / _RoBlindDistance;
					o.color.rgb = 1 * clamp(1 * d, -1, 1);
                #else
                    o.color.rgb = float3(1,1,1);
				#endif


                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                fixed a = 0.5625;
                float env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
                env = env * 0.5 + 0.5;
                //col = col * 0.88;// * 0.5833333333333333;

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col.rgb * i.color.rgb * env * a, a);
            }
            ENDCG
        }
    }
}
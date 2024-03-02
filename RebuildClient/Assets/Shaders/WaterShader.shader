Shader "Unlit/RoWaterShader"
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
            "Queue" = "Transparent-1"
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

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
    
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

            v2f vert (appdata v)
            {
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float4 vin = v.vertex;

                float offset = (_Time.x * 1000 * _WaveSpeed) % 360 - 180;
                float x = worldPos.x % 2.0;
                float y = worldPos.z % 2.0;

                float diff    = x < 1.0 ? y < 1.0 ? 1.0 : -1.0 : 0.0;
                
                worldPos.y += sin((3.1415926/180) * (offset + 0.5 * _WavePitch * (worldPos.x + worldPos.z + diff))) * _WaveHeight;

                v.vertex = mul( unity_WorldToObject, worldPos);

                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, UnityObjectToClipPos(vin));


                float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
                o.vertex = mul(UNITY_MATRIX_P, view);
    

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                    //float z = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                    //float sceneZ = LinearEyeDepth();
                    //float depth = sceneZ - i.screenPos.z;
                //float depth = Linear01Depth(z);

                //if (depth < i.screenPos.z)
                //    discard;

                //float depth = i.screenPos.z;

                //return float4(depth, depth, depth, 1);
    //return fixed4(i.uv.x%1, i.uv.y%1, 0, 1);
    
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);

                fixed a = 0.5625;
                //col = col * 0.88;// * 0.5833333333333333;
                
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col.rgb*a, a);
            }
            ENDCG
        }
    }
}

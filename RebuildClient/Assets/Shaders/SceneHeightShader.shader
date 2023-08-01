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
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Ztest always
        
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _WaveHeight;
            float _WavePitch;
            float _WaveSpeed;
            float4 _Color;

            v2f vert(appdata v)
            {
                
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float4 vin = v.vertex;

                float offset = (_Time.x * 1000 * _WaveSpeed) % 360 - 180;
                float x = worldPos.x % 2.0;
                float y = worldPos.z % 2.0;

                float diff = x < 1.0 ? y < 1.0 ? 1.0 : -1.0 : 0.0;
    
                float pos = worldPos.y + sin((3.1415926 / 180) * (offset + 0.5 * _WavePitch * (worldPos.x + worldPos.z + diff))) * _WaveHeight;
                
                worldPos.y += sin((3.1415926 / 180) * (offset + 0.5 * _WavePitch * (worldPos.x + worldPos.z + diff))) * _WaveHeight;
                
                v.vertex = mul(unity_WorldToObject, worldPos);

                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 view = mul(UNITY_MATRIX_V, float4(worldPos, 1));
                o.vertex = mul(UNITY_MATRIX_P, view);
    
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        
                o.screenPos = float4(v.uv.x % 1, v.uv.y % 1, pos, 1);

    
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(i.uv.x%1, i.uv.y%1, i.screenPos.z, 1);
            }
            ENDCG
        }
    }
}

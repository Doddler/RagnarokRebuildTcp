Shader "Unlit/GroundBlendTest"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Lightmap("Lightmap", 2D) = "white" {}
        _Ambient("Ambient", Color) = (1,1,1,1)
        _Diffuse("Diffuse", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
            float4 _MainTex_ST;
            sampler2D _Lightmap;
            float4 _Lightmap_ST;

            fixed4 _Ambient;
            fixed4 _Diffuse;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 lm = tex2D(_Lightmap, i.uv/30);
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col + lm);

                //lm *= lm.a;

                //return col * _Ambient + lm;

                return lm;

                fixed4 light = saturate(_Ambient + _Diffuse)/2 * lm.a;

                return col * light + lm;


                //return a * lm;
                return col*_Ambient*lm.a+lm;
            }
            ENDCG
        }
    }
}

Shader "Custom/ObjectShaderAcceptProjector"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _AmbientTex("Ambient (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Specular("Specular", Range(0,1)) = 0.0
        _Cutoff("Cutoff", Range(0,1)) = 0.5
        _AmbientIntensity("Ambient Intensity", Range(0,1)) = 1
        _LightmapIntensity("Lightmap Intensity", Range(0,1)) = 1
    }
        SubShader
        {
                Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "False" "RenderType" = "TransparentCutout" "BW" = "TrueProbes" "LightMode" = "ForwardBase"}

            LOD 200
            //Lighting Off

    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        // make fog work
        #pragma multi_compile_fog
        #pragma multi_compile_fwdbase
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma target 2.0
        #pragma multi_compile_fog




        #include "UnityCG.cginc"
        #include "AutoLight.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
            float4 color    : COLOR;
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
            float3 normal : NORMAL;


        };

        struct v2f
        {
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
            float4 color    : COLOR;
            float4 pos : SV_POSITION;
            float3 normal : TEXCOORD2;
            float3 lightDir : TEXCOORD3;

            UNITY_FOG_COORDS(4)
            LIGHTING_COORDS(5, 6)

        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Color;
        fixed _Cutoff;
        fixed _AmbientIntensity;
        fixed _LightmapIntensity;


#ifdef DIRLIGHTMAP_COMBINED
            SamplerState samplerunity_LightmapInd;
#endif



            //unity defined variables
            uniform float4 _LightColor0;

            //from our globals
            float4 _RoAmbientColor;
            float4 _RoDiffuseColor;
            float _RoLightmapAOStrength;
            float _Opacity;


            float4 Screen(float4 a, float4 b)
            {
                return 1 - (1 - a) * (1 - b);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.normal = normalize(v.normal).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightDir = normalize(ObjSpaceLightDir(v.vertex));
                o.uv2 = v.uv2.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                //o.worldpos = mul(unity_ObjectToWorld, v.vertex);


                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 diffuse = tex2D(_MainTex, i.uv);


                clip(diffuse.a - _Cutoff);

#if LIGHTMAP_ON
                float4 lm = float4(DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv2)), 1) * _LightmapIntensity;
#else
                float4 lm = float4(ShadeSH9(float4(i.normal, 1)),1);
#endif
                float4 ambienttex = float4(1, 1, 1, 1);

#ifdef DIRLIGHTMAP_COMBINED
                ///float4 lm2 = float4(DecodeLightmap(UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, i.uv2)), 1);

                fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_LightmapInd, i.uv2);
                //return float4(bakedDirTex.r, 1, 1, 1);
                ambienttex = bakedDirTex.rrrr;

#endif
                float3 L = normalize(i.lightDir);
                float3 N = normalize(i.normal);

                float attenuation = LIGHT_ATTENUATION(i);
                float4 ambient = _RoAmbientColor;


                float shadowStr = saturate(attenuation * _Opacity + (1 - _Opacity));

                float NdotL = saturate(dot(N, L));
                float4 diffuseTerm = NdotL * shadowStr; //* _DiffuseTint 

                lm *= (0.5 + saturate(NdotL * 2) * 0.5);

                fixed ambientStrength = _RoLightmapAOStrength * _AmbientIntensity;

                ambienttex = ambienttex * ambientStrength + (1 - ambientStrength);


                float4 finalColor = diffuse; // *(ambient + diffuseTerm);

                float env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));

                finalColor = saturate(NdotL * _RoDiffuseColor + clamp(_RoAmbientColor, 0, 0.5)) * shadowStr * diffuse * env + lm * 2 * (diffuse);;

                finalColor *= ambienttex;

                finalColor *= 1 + (ambientStrength / 10);

                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                return finalColor;

            }

            ENDCG
        }

        }
            FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}

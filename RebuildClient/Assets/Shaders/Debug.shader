// THIS SHADER FILE SHOULDN'T BE USED FOR ANYTHING IMPORTANT!
// IT'S INTENDED FOR TESTING ONLY!

 Shader "Projector/AdditiveTint2" {
     Properties {
         _Color ("Tint Color", Color) = (1,1,1,1)
         _Attenuation ("Falloff", Range(0.0, 1.0)) = 1.0
         _ShadowTex ("Cookie", 2D) = "gray" {}
     }
     Subshader {
         Tags {"Queue"="Transparent-1"}
         Pass {
             ZWrite Off
             ColorMask RGB
             Blend SrcAlpha OneMinusSrcAlpha // Additive blending
             Offset 0, 0
             Ztest Equal
 
             CGPROGRAM
             #pragma vertex vert
             #pragma fragment frag
             #include "UnityCG.cginc"
             
             struct v2f {
                 float4 uvShadow : TEXCOORD0;
                 float4 pos : SV_POSITION;
             };
             
             float4x4 unity_Projector;
             float4x4 unity_ProjectorClip;
             
             v2f vert (float4 vertex : POSITION)
             {
                 v2f o;
                 o.pos = UnityObjectToClipPos (vertex);
                 o.uvShadow = mul (unity_Projector, vertex);
                 return o;
             }
             
             sampler2D _ShadowTex;
             fixed4 _Color;
             float _Attenuation;
             
             fixed4 frag (v2f i) : SV_Target
             {
                 // Apply alpha mask
                 fixed4 texCookie = tex2Dproj (_ShadowTex, UNITY_PROJ_COORD(i.uvShadow));
                 float4 outColor = _Color * texCookie;
                 //float4 color = float4(_Color.rgb, texCookie.a);
                 //float4 outColor = 1 - (1 - color) * (1 - texCookie);
                 //return outColor.a = texCookie.a * _Color.a;
                 
                 //outColor.rgb = _Color.grb;
                 //outColor.a = texCookie.a;
                 
                 //outColor.rgb *= outColor.a;
                 
                 // Attenuation
                 float depth = i.uvShadow.z; // [-1 (near), 1 (far)]
                 return outColor * clamp(1.0 - abs(depth) + _Attenuation, 0.0, 1.0);
             }
             ENDCG
         }
     }
 }
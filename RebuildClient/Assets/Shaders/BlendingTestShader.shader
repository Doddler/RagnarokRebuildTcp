Shader "Unlit/BlendingTestShader"
{
    Properties
    {

    }
    SubShader
    {
    Tags
            {
                "RenderType" = "Transparent""Queue" = "Transparent"
            }

            Cull Off
            Lighting Off
            ZWrite Off         
        Blend One OneMinusSrcAlpha
        //Blend SrcAlpha OneMinusSrcAlpha

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
                float4 color    : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 color    : COLOR;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                
                // compute depth
                o.screenPos = ComputeScreenPos(o.vertex);
                COMPUTE_EYEDEPTH(o.screenPos.z);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
//                fixed4 col = tex2D(_MainTex, i.uv);

            float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.screenPos);  // sample from depth texture
            depth = Linear01Depth(depth);

            clip(0.01 - depth);

            return i.color; // *depth;
            }
            ENDCG
        }
    }
}

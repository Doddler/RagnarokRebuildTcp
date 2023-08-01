
Shader "Unlit/BillboardVerticalZDepthDirect"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
 
    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True" }
 
        ZWrite Off
        Offset -10, -10
        Blend SrcAlpha OneMinusSrcAlpha
 
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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST;
 
            float rayPlaneIntersection( float3 rayDir, float3 rayPos, float3 planeNormal, float3 planePos)
            {
                float denom = dot(planeNormal, rayDir);
                denom = max(denom, 0.000001); // avoid divide by zero
                float3 diff = planePos - rayPos;
                return dot(diff, planeNormal) / denom;
            }
 
            v2f vert(appdata v)
            {
                v2f o;
    
                float3 scale = float3(
					length(unity_ObjectToWorld._m00_m10_m20),
					length(unity_ObjectToWorld._m01_m11_m21),
					length(unity_ObjectToWorld._m02_m12_m22)
				);
 
                unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
                unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
                unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);
    
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv.xy;
 
                // billboard mesh towards camera
                float3 vpos = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                float4 worldCoord = float4(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23, 1);
                float4 viewPos = mul(UNITY_MATRIX_V, worldCoord) + float4(vpos, 0);
 
                o.pos = mul(UNITY_MATRIX_P, viewPos);
 
                // calculate distance to vertical billboard plane seen at this vertex's screen position
                float3 planeNormal = normalize(float3(UNITY_MATRIX_V._m20, 0.0, UNITY_MATRIX_V._m22));
                float3 planePoint = unity_ObjectToWorld._m03_m13_m23;
                float3 rayStart = _WorldSpaceCameraPos.xyz;
                float3 rayDir = -normalize(mul(UNITY_MATRIX_I_V, float4(viewPos.xyz, 1.0)).xyz - rayStart); // convert view to world, minus camera pos
                float dist = rayPlaneIntersection(rayDir, rayStart, planeNormal, planePoint);
 
                // calculate the clip space z for vertical plane
                float4 planeOutPos = mul(UNITY_MATRIX_VP, float4(rayStart + rayDir * dist, 1.0));
                float newPosZ = planeOutPos.z / planeOutPos.w * o.pos.w;
 
                // use the closest clip space z
                #if defined(UNITY_REVERSED_Z)
                o.pos.z = max(o.pos.z, newPosZ);
                #else
                o.pos.z = min(o.pos.z, newPosZ);
                #endif
 
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
 
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
 
                return col;
            }
            ENDCG
        }
    }
}
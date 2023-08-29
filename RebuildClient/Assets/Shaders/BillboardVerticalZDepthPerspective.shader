
Shader "Unlit/BillboardVerticalZDepthPerspective"
{
    Properties
    {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _Color("Tint", Color) = (1,1,1,1)
		[PerRendererData] _Offset("Offset", Float) = 0
		[PerRendererData] _Width("Width", Float) = 0
    }
 
    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True" }
 
        ZWrite Off
        Offset -10, -10
        Blend One OneMinusSrcAlpha
        
        
        Pass
        {
            ZWrite On
			Blend Zero One
            Offset 0, 0
            
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
                float2 uv : TEXCOORD0;
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				half4  worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _WaterDepth;
			sampler2D _WaterImageTexture;
			float4 _WaterImageTexture_ST;

            
			//from our globals
			float4 _RoAmbientColor;
			float4 _RoDiffuseColor;

            
			fixed4 _Color;
 
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
            	v.vertex.z -= 2;
 
                unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
                unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
                unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);
    
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv.xy;
 
				// billboard mesh towards camera
				float3 vpos = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
				float4 worldCoord = float4(unity_ObjectToWorld._m03_m13_m23, 1);
				float4 viewPivot = mul(UNITY_MATRIX_V, worldCoord);
				 
				// construct rotation matrix
				float3 forward = -normalize(viewPivot);
				float3 up = mul(UNITY_MATRIX_V, float3(0,1,0)).xyz;
				float3 right = normalize(cross(up,forward));
				up = cross(forward,right);
				float3x3 facingRotation = float3x3(right, up, forward);
				 
				float4 viewPos = float4(viewPivot + mul(vpos, facingRotation), 1.0);
  
                o.pos = mul(UNITY_MATRIX_P, viewPos);
 
                // calculate distance to vertical billboard plane seen at this vertex's screen position
                float3 planeNormal = normalize((_WorldSpaceCameraPos.xyz - unity_ObjectToWorld._m03_m13_m23) * float3(1,0,1));
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

                o.color = v.color * _Color;

                return o;
            }
 
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                col *= i.color;
                
				clip(col.a - 0.5);
 
                return col;
            }
            ENDCG
        }
 
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
                float2 uv : TEXCOORD0;
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				half4  worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            
			float4 _ClipRect;
            sampler2D _MainTex;
            float4 _MainTex_ST;
			float4 _MainTex_TexelSize;

            sampler2D _WaterDepth;
			sampler2D _WaterImageTexture;
			float4 _WaterImageTexture_ST;

            
			//from our globals
			float4 _RoAmbientColor;
			float4 _RoDiffuseColor;


            fixed _Width;
			fixed4 _Color;
 
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
				float4 worldCoord = float4(unity_ObjectToWorld._m03_m13_m23, 1);
				float4 viewPivot = mul(UNITY_MATRIX_V, worldCoord);
				 
				// construct rotation matrix
				float3 forward = -normalize(viewPivot);
				float3 up = mul(UNITY_MATRIX_V, float3(0,1,0)).xyz;
				float3 right = normalize(cross(up,forward));
				up = cross(forward,right);
				float3x3 facingRotation = float3x3(right, up, forward);
				 
				float4 viewPos = float4(viewPivot + mul(vpos, facingRotation), 1.0);
  
                o.pos = mul(UNITY_MATRIX_P, viewPos);
 
                // calculate distance to vertical billboard plane seen at this vertex's screen position
                float3 planeNormal = normalize((_WorldSpaceCameraPos.xyz - unity_ObjectToWorld._m03_m13_m23) * float3(1,0,1));
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

                o.color = v.color * _Color;
            	            	
				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
				float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
				o.uv = float4(v.uv.x, v.uv.y, maskUV.x, maskUV.y);

            	//end of smooth pixel
				#ifndef WATER_OFF

            		//build info needed for water line
					float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.x, v.vertex.y*1.5, 0, 1)).xyz; //fudge y sprite height 
					o.screenPos = ComputeScreenPos(o.pos);
					o.worldPos = float4(v.vertex.x, worldPos.y, 0, 0);
				#endif
						
 
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
 
            fixed4 frag(v2f i) : SV_Target
            {
                float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
				env = env * 0.5 + 0.5;
            	env = float4(1,1,1,1);
                                
                // fixed4 col = tex2D(_MainTex, i.uv);
                
				//smoothpixel
				// apply anti-aliasing
				float2 texturePosition = i.uv * _MainTex_TexelSize.zw;
				float2 nearestBoundary = round(texturePosition);
				float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
					abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));
	
				float2 samplePosition = (texturePosition - nearestBoundary) / delta;
				samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

				fixed4 diff = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);
				//endsmoothpixel

            	

				fixed4 col = diff * i.color * float4(env.rgb,1);
            	

            	UNITY_APPLY_FOG(i.fogCoord, col);

                //col *= i.color; // * float4(env.rgb,1);
            	
				col.rgb *= col.a;


            	
				#ifndef WATER_OFF
					float2 uv = (i.screenPos.xy / i.screenPos.w);
					float4 water = tex2D(_WaterDepth, uv);
					float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);
	
					if (water.a < 0.1)
						return col;
	
					float4 waterTex = tex2D(_WaterImageTexture, wateruv);
					float height = water.z;
					
					waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex / 2);
	
					// apply fog
					UNITY_APPLY_FOG(i.fogCoord, waterTex);
	
					float simHeight = i.worldPos.y - abs(i.worldPos.x)/(_Width)*0.5;
	
					simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);
	
					if (height-0 > simHeight)
						col.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));
					//c.rgb *= waterTex.rgb;
	
				#endif
 
                return col;
            }
            ENDCG
        }
    }
}
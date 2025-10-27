
Shader "Unlit/BillboardVerticalZDepthDirect"
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
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" /*"DisableBatching" = "True"*/ "LightMode"="ForwardBase" }
 
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Cull Off
        Stencil
        {
            Ref 1
            Comp NotEqual
        }
/*
        Pass
        {
            ZWrite On
			Blend Zero One
            
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
            
            fixed _Offset;
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

            	planePoint.z += 0.5f;

				// added check to get distance to an arbitrary ground plane
				float groundDist = rayPlaneIntersection(rayDir, rayStart, float3(0,1,0), planePoint);
				// use "min" distance to either plane (I think the distances are actually negative)
				dist = min(dist, groundDist);

				// then do the rest of the shader normally
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
 
                UNITY_TRANSFER_FOG(o,o.pos);

	
                
                return o;
            }
 
            fixed4 frag(v2f i) : SV_Target
            {
                float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
				env = env * 0.5 + 0.5;

                                
                fixed4 col = tex2D(_MainTex, i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);

                col *= i.color * float4(env.rgb,1);
				//c.rgb *= c.a;

                
				clip(col.a - 0.5);
 
                return col;
            }
            ENDCG
        }
 */

        Pass
        {
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
			#pragma multi_compile _ LIGHTPROBE_SH
            #pragma multi_compile _ BLINDEFFECT_ON
            
            #include "UnityCG.cginc"
            #include "Billboard.cginc"

            #pragma multi_compile_instancing
            
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
#if BLINDEFFECT_ON
				fixed4 color : COLOR0;
				fixed4 color2 : COLOR1;
#else
				fixed4 color : COLOR;
#endif
                float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				half4  worldPos : TEXCOORD2;
				half4  envColor : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            sampler2D _WaterDepth;
			sampler2D _WaterImageTexture;
			float4 _WaterImageTexture_ST;

            
			//from our globals
			float4 _RoAmbientColor;
			float4 _RoDiffuseColor;

            
			#ifdef BLINDEFFECT_ON
				float4 _RoBlindFocus;
				float _RoBlindDistance;
			#endif
            
            fixed _Width;
            fixed _Offset;
			fixed4 _Color;
            fixed4 _ClipRect;
            
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
				v2f o;

                Billboard billboard = GetBillboard(v.vertex, _Offset);
				o.pos = billboard.positionCS;
                float3 viewPos = billboard.positionVS;

				// calculate distance to vertical billboard plane seen at this vertex's screen position
				float3 planeNormal = normalize(float3(UNITY_MATRIX_V._m20, 0.0, UNITY_MATRIX_V._m22));
				float3 planePoint = unity_ObjectToWorld._m03_m13_m23;
				float3 rayStart = _WorldSpaceCameraPos.xyz;
				float3 rayDir = -normalize(mul(UNITY_MATRIX_I_V, float4(viewPos.xyz, 1.0)).xyz - rayStart); // convert view to world, minus camera pos
				float dist = rayPlaneIntersection(rayDir, rayStart, planeNormal, planePoint);
            	planePoint.y += _Offset/4;
				 
				// added check to get distance to an arbitrary ground plane
				float groundDist = rayPlaneIntersection(rayDir, rayStart, float3(0,1,0), planePoint);
				// use "min" distance to either plane (I think the distances are actually negative)
				dist = max(dist, groundDist);
				 
				// then do the rest of the shader normally
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
            	o.envColor = float4(ShadeSH9(fixed4(0,1,0,1)),1);
            	//o.envColor = o.envColor * 0.7;

            	
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
						

				UNITY_TRANSFER_FOG(o,UnityObjectToClipPos(v.vertex));
				return o;
            }
 
            fixed4 frag(v2f i) : SV_Target
            {
                float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
				env = env * 0.5 + saturate(0.5 + i.envColor);
                
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

				//UNITY_APPLY_FOG(i.fogCoord, col);            	

                //col *= i.color * float4(env.rgb,1);
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
					//UNITY_APPLY_FOG(i.fogCoord, waterTex);
	
					float simHeight = i.worldPos.y - abs(i.worldPos.x)/(_Width)*0.5;
	
					simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);
	
					if (height-0 > simHeight)
						col.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));
					//c.rgb *= waterTex.rgb;
	
				#endif

            	#ifdef BLINDEFFECT_ON
				col.rgb *= i.color2; // * (0.5 + i.color2/2);
				#endif
 
                return col;
            }
            ENDCG
        }
    }
}
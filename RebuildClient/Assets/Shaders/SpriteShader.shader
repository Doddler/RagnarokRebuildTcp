// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader"Ragnarok/CharacterSpriteShader"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        //[PerRendererData] _PalTex("Palette Texture", 2D) = "white" {}
        [PerRendererData] _Color("Tint", Color) = (1,1,1,1)
        [PerRendererData] _EnvColor("Environment", Color) = (1,1,1,1)
        [PerRendererData] _Offset("Offset", Float) = 0
        [PerRendererData] _Width("Width", Float) = 0
        [PerRendererData] _VPos("VerticalPos", Float) = 0
        //[PerRendererData] _LightingSamplePosition("LightingSamplePosition", Vector) = (0,0,0,0)
        _ColorDrain("Color Drain", Range(0,1)) = 0
        _Rotation("Rotation", Range(0,360)) = 0
    }

    SubShader
    {

        Tags
        {
            "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        
        CGINCLUDE
        #include "UnityCG.cginc"
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap

        #pragma multi_compile _ GROUND_ITEM

        float4 _MainTex_TexelSize;
        fixed _VPos;
        
        #ifdef GROUND_ITEM
        struct InstanceData
        {
            float4 color;
            float4 uvRect;
            float offset;
            float colorDrain;
        };

        StructuredBuffer<InstanceData> _Instances;
        int _BaseInstance;

        inline void SetupInstancingData
        (
            uint instanceID, uint vertexID,
            inout float3 positionOS,
            inout float2 uv,
            inout float4 vcol,
            inout float4 color,
            inout float isHidden,
            inout float offset,
            inout float colorDrain)
        {
            InstanceData inst = _Instances[_BaseInstance + instanceID];

            //positionOS = inst.positionOS[vertexID];
            float4 rect = inst.uvRect;
            uv = rect.xy * _MainTex_TexelSize.xy + uv * rect.zw * _MainTex_TexelSize.xy;

            color = inst.color;
            offset = inst.offset;
            colorDrain = inst.colorDrain;
        }
        #else
        struct InstanceData
        {
            float3 positionOS[4];
            float2 uv[4];
            float4 vcol[4];

            float4 color;

            float isHidden;
            float offset;
            float colorDrain;
            float vPos;
        };

        StructuredBuffer<InstanceData> _Instances;
        int _BaseInstance;

        inline void SetupInstancingData
        (
            uint instanceID, uint vertexID,
            inout float3 positionOS,
            inout float2 uv,
            inout float4 vcol,
            inout float4 color,
            inout float isHidden,
            inout float offset,
            inout float colorDrain,
            inout float vPos)
        {
            InstanceData inst = _Instances[_BaseInstance + instanceID];

            positionOS = inst.positionOS[vertexID];
            uv = inst.uv[vertexID];
            vcol = inst.vcol[vertexID];

            color = inst.color;
            isHidden = inst.isHidden;
            offset = inst.offset;
            colorDrain = inst.colorDrain;
            vPos = inst.vPos;
        }
        #endif
        ENDCG

        Pass // Depth Only
        {
            Name "DepthOnly"
            ZWrite On
            ColorMask 0
            AlphaToMask On
            
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Billboard.cginc"

            #pragma multi_compile _ INSTANCING_ON

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                #ifdef INSTANCING_ON
                uint vid : SV_VertexID;
                uint instanceID : SV_InstanceID;
                #endif
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                #ifdef INSTANCING_ON
                float isHidden = 0;
                float _Offset = 0;
                float _ColorDrain = 0;
                SetupInstancingData(v.instanceID, v.vid, v.vertex.xyz, v.texcoord.xy, v.color, _Color, isHidden, _Offset, _ColorDrain);
                #endif

                v2f o;

                Billboard billboard = GetBillboard(v.vertex, 0);
                o.pos = billboard.positionCS;

                #ifdef  INSTANCING_ON
                o.pos.z += 0.001;
                #endif
                
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord);
                c *= i.color;

                clip(c.a - 0.5);

                return c;
            }
            ENDCG
        }

        Pass // Color
        {
            Name "Color"
            //Tags {"LightMode" = "Color"}
            //AlphaToMask On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            //#pragma multi_compile _ PIXELSNAP_ON
            //#pragma multi_compile _ PALETTE_ON
            #pragma multi_compile _ SMOOTHPIXEL
            #pragma multi_compile _ BLINDEFFECT_ON
            #pragma shader_feature _ WATER_OFF
            #pragma shader_feature _ COLOR_DRAIN
            #pragma multi_compile _ INSTANCING_ON

            //#define SMOOTHPIXEL

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Billboard.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                #ifdef INSTANCING_ON
                uint vid : SV_VertexID;
                uint instanceID : SV_InstanceID;
                #endif
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                #if BLINDEFFECT_ON
                fixed4 color : COLOR0;
                fixed4 color2 : COLOR1;
                #else
                fixed4 color : COLOR;
                fixed4 lighting : COLOR2;
                #endif
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half4 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _EnvColor;
            fixed _Offset;
            fixed _Rotation;
            fixed _Width;
            fixed _ColorDrain;

            float4 _ClipRect;

            float3 _LightingSamplePosition;
            float _IsMeshRenderer;

            sampler2D _MainTex;
            float4 _MainTex_ST;
            // sampler2D _PalTex;

            sampler2D _WaterDepth;
            sampler2D _WaterImageTexture;
            float4 _WaterImageTexture_ST;

            //float _MaskSoftnessX;
            //float _MaskSoftnessY;

            // Should we use a global CBuffer? would it even be worth adding the complexity?
            //from our globals
            float4 _RoAmbientColor;
            float4 _RoDiffuseColor;

            float4 unity_Lightmap_ST;

            #ifdef BLINDEFFECT_ON
            float4 _RoBlindFocus;
            float _RoBlindDistance;
            #endif

            float3 ShadeVertexLightsSprite(float3 pos)
            {
                float3 viewpos = UnityWorldToViewPos(pos);

                float3 lightColor = UNITY_LIGHTMODEL_AMBIENT.xyz;
                UNITY_UNROLL
                for (int i = 0; i < 8; i++)
                {
                    float3 toLight = unity_LightPosition[i].xyz - viewpos.xyz * unity_LightPosition[i].w;

                    float lengthSq = dot(toLight, toLight);

                    lengthSq = max(lengthSq, 0.000001);
                    toLight *= rsqrt(lengthSq);

                    float atten = rcp(1.0 + lengthSq * unity_LightAtten[i].z);

                    // Spot light support.
                    float rho = max(0, dot(toLight, unity_SpotDirection[i].xyz));
                    float spotAtt = (rho - unity_LightAtten[i].x) * unity_LightAtten[i].y;
                    atten *= saturate(spotAtt);

                    // unity_LightPosition[i].w will be 0 for directional lights
                    lightColor += unity_LightColor[i].rgb * atten * unity_LightPosition[i].w;
                }
                return lightColor;
            }

            v2f vert(appdata_t v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v)
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v)

                #ifdef INSTANCING_ON
                float isHidden = 0;
                SetupInstancingData(v.instanceID, v.vid, v.vertex.xyz, v.texcoord.xy, v.color, _Color, isHidden, _Offset, _ColorDrain);
                #endif

                Billboard billboard = GetBillboard(v.vertex, _Offset);
                float3 worldPos = billboard.positionWS;

                o.vertex = billboard.positionCS;
                o.color = v.color * _Color;

                #ifdef  INSTANCING_ON
                o.vertex.z += 0.001;
                #endif
                
                float4 tempVertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, tempVertex);

                //smoothpixelshader stuff here
                #ifdef SMOOTHPIXEL
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                o.texcoord = float4(v.texcoord.x, v.texcoord.y, maskUV.x, maskUV.y);
                #else
                o.texcoord = v.texcoord;
                #endif

                // It's cheap enougth that we can just perform the calculation for every vertex.
                // For smoother lighting use billboard.positionWS as the sampling pos.
                float3 samplingPos = _IsMeshRenderer > 0.5? _LightingSamplePosition: mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                o.lighting = float4(ShadeVertexLightsSprite(samplingPos), 1.0);

                /*//end of smooth pixel
                #ifndef WATER_OFF

                    //this mess fully removes the rotation from the matrix	
                    float3 scale = float3(
                        length(unity_ObjectToWorld._m00_m10_m20),
                        length(unity_ObjectToWorld._m01_m11_m21),
                        length(unity_ObjectToWorld._m02_m12_m22)
                    );

                    unity_ObjectToWorld._m00_m10_m20 = float3(scale.x, 0, 0);
                    unity_ObjectToWorld._m01_m11_m21 = float3(0, scale.y, 0);
                    unity_ObjectToWorld._m02_m12_m22 = float3(0, 0, scale.z);

                    //build info needed for water line
                    worldPos = mul(unity_ObjectToWorld, float4(billboard.positionCS.x, billboard.positionCS.y * 1.5, 0, 1)).xyz; //fudge y sprite height 
                    o.screenPos = ComputeScreenPos(o.vertex);
                    o.worldPos = float4(billboard.positionCS.x, worldPos.y, 0, 0);
                #endif*/

                #if BLINDEFFECT_ON
                float d = distance(worldPos, _RoBlindFocus);
                //d = 1.2 - d / _RoBlindDistance;
                d = 1.5 - (d / _RoBlindDistance) * 1.5 + clamp((_RoBlindDistance - 50) / 120, -0.2, 0);
                o.color2.rgb = clamp(1 * d, -1, 1);
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                //return float4(i.lighting.rgb, 1);
                //return float4(i.texcoord.xy, 0, 1);
                //return float4(frac(_LightingSamplePosition * 0.5), 1);
                //environment ambient contribution disabled for now as it muddies the sprite
                //todo: turn ambient contribution back on if fog is disabled.
                float4 env = float4(1, 1, 1, 1);
                //return i.color;
                //float4 env = 1 - ((1 - _RoDiffuseColor) * (1 - _RoAmbientColor));
                //env = env * 0.3 + 0.7;// + saturate(0.5 + i.envColor);

                //smoothpixel
                // apply anti-aliasing
                #ifdef SMOOTHPIXEL
                float2 texturePosition = i.texcoord * _MainTex_TexelSize.zw;
                float2 nearestBoundary = round(texturePosition);
                float2 delta = float2(abs(ddx(texturePosition.x)) + abs(ddx(texturePosition.y)),
                                    abs(ddy(texturePosition.x)) + abs(ddy(texturePosition.y)));

                float2 samplePosition = (texturePosition - nearestBoundary) / delta;
                samplePosition = clamp(samplePosition, -0.5, 0.5) + nearestBoundary;

                fixed4 diff = tex2D(_MainTex, samplePosition * _MainTex_TexelSize.xy);
                #else
                fixed4 diff = tex2D(_MainTex, i.texcoord.xy);

                #endif
                // fixed4 diff = tex2D(_MainTex,i.texcoord.xy);
                //endsmoothpixel

                // //#ifdef PALETTE_ON
                // diff *= 256;
                // diff = floor(diff);
                // diff /= 256;
                // diff = float4(tex2D(_PalTex, float2((diff.r+diff.g+diff.b)/3, 0.5)).rgb, diff.a);
                // //#endif

                // fixed4 lm = UNITY_SAMPLE_TEX2D(unity_Lightmap, i.light.xy);
                // half3 bakedColor = DecodeLightmap(lm);

                //return float4(i.color.rgb/2, 1);

                //The UNITY_APPLY_FOG can't be called twice so we'll store it to re-use later
                float4 fogColor = float4(1, 1, 1, 1);

                float avg = (diff.r + diff.g + diff.b) / 3;
                diff.rgb = lerp(diff.rgb, float3(avg, avg, avg), _ColorDrain);
                diff.rgb += diff * i.lighting;

                fixed4 c = diff * min(1.35, fogColor * i.color * float4(env.rgb, 1));
                c = saturate(c);

                clip(c.a - 0.001);

                #ifndef WATER_OFF
                float2 uv = (i.screenPos.xy / i.screenPos.w);
                float4 water = tex2D(_WaterDepth, uv);
                float2 wateruv = TRANSFORM_TEX(water.xy, _WaterImageTexture);

                if (water.a < 0.1)
                    return c;

                float4 waterTex = tex2D(_WaterImageTexture, wateruv);
                float height = water.z;

                waterTex = float4(0.5, 0.5, 0.5, 1) + (waterTex * 0.6);

                float simHeight = i.worldPos.y - abs(i.worldPos.x) / (_Width) * 0.5;

                simHeight = clamp(simHeight, i.worldPos.y - 0.4, i.worldPos.y);
                waterTex *= fogColor;

                if (height - 0 > simHeight)
                    c.rgb *= lerp(float3(1, 1, 1), waterTex.rgb, saturate(((height - 0) - simHeight) * 10));

                #endif

                UNITY_APPLY_FOG(i.fogCoord, c);
                c.rgb *= c.a;

                #ifdef BLINDEFFECT_ON
                c.rgb = saturate(c.rgb * i.color2);
                #endif

                return c;
            }
            ENDCG
        }

        /*Pass // X-Ray
        {
            Name "XRay"
            ZWrite Off
            ZTest Greater

            Offset -10, 1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Billboard.cginc"

            #pragma multi_compile _ INSTANCING_ON

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                #ifdef INSTANCING_ON
                uint vid : SV_VertexID;
                uint instanceID : SV_InstanceID;
                #endif
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Offset;

            v2f vert(appdata_t v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                #ifdef INSTANCING_ON
                float isHidden = 0;
                float _Offset = 0;
                float _ColorDrain = 0;
                SetupInstancingData(v.instanceID, v.vid, v.vertex.xyz, v.texcoord.xy, v.color, _Color, isHidden, _Offset, _ColorDrain);
                #endif

                v2f o;

                Billboard billboard = GetBillboard(v.vertex, _Offset);
                o.pos = billboard.positionCS;

                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord);
                c *= i.color;
                c.rgb *= c.a;

                clip(frac(i.pos.x / 2) - 0.5);
                clip(frac(i.pos.y / 2) - 0.5);

                return c;
            }
            ENDCG
        }*/
    }
}
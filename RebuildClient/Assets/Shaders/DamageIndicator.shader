Shader "Unlit/DamageIndicator"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass // Damage Indicators
        {
            Name "Damage Indicators - Render"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 3.5
            #pragma target 4.5 DI_STRUCTURED_BUFFER
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap
            #pragma multi_compile_local _ DI_STRUCTURED_BUFFER DI_CBUFFER_INSTANCING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #if defined(DI_STRUCTURED_BUFFER) && (SHADER_TARGET >= 45)
            #define DI_STRUCTURED_ACTIVE
            #endif

            #ifdef DI_STRUCTURED_ACTIVE
            struct DamageIndicatorData
            {
                float alpha;
                int value;
                float4 color;
                float lifeTime;
                float critJitter;
                uint flags;
            };

            bool IsCrit(DamageIndicatorData d) { return (d.flags & 1 << 0) != 0; }
            bool IsMiss(DamageIndicatorData d) { return (d.flags & 1 << 1) != 0; }
            bool IsAgi(DamageIndicatorData d) { return (d.flags & 1 << 2) != 0; }
            bool IsSlow(DamageIndicatorData d) { return (d.flags & 1 << 3) != 0; }
            bool IsExp(DamageIndicatorData d) { return (d.flags & 1 << 4) != 0; }

            StructuredBuffer<DamageIndicatorData> _Instances;
            int _BaseInstance;
            #endif

            #ifdef DI_CBUFFER_INSTANCING
            UNITY_INSTANCING_BUFFER_START(DamageIndicatorProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DIColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DIParams0)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DIParams1)
            UNITY_INSTANCING_BUFFER_END(DamageIndicatorProps)
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 index : TEXCOORD1;
                float4 vcol : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                half4 color : COLOR0;
                float alpha : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Spacing;
            CBUFFER_END

            #if !defined(DI_STRUCTURED_BUFFER) && !defined(DI_CBUFFER_INSTANCING)
            float4 _Color;
            float _Alpha;
            bool _IsCrit;
            bool _IsMiss;
            bool _IsAgi;
            bool _IsSlow;
            bool _IsExp;
            int _Value;
            float _LifeTime;
            float _CritJitter;
            #endif

            struct DamageIndicator
            {
                int digits[5];
                int count;
                int magnitude;
            };

            inline DamageIndicator GetDamageIndicator(int n)
            {
                DamageIndicator di;

                uint un = (uint)abs(n);

                int mag = 0;
                uint scaled = un;
                if (un > 999999u)
                {
                    mag = 2;
                    scaled = un / 1000000u;
                }
                else if (un > 99999u)
                {
                    mag = 1;
                    scaled = un / 1000u;
                }

                UNITY_UNROLL
                for (int i = 0; i < 5; i++)
                {
                    di.digits[i] = -1;
                }

                if (scaled == 0u)
                {
                    di.digits[4] = 0;
                    di.count = 1;
                    di.magnitude = mag;
                    return di;
                }

                uint tmp = scaled;
                int idx = 4;
                UNITY_UNROLL
                for (int i = 0; i < 5 && tmp > 0u; i++)
                {
                    di.digits[idx--] = (int)(tmp % 10u);
                    tmp /= 10u;
                }

                int cnt = 0;
                [unroll]
                for (int i = 0; i < 5; i++)
                    cnt += (di.digits[i] >= 0) ? 1 : 0;

                di.count = cnt;
                di.magnitude = mag;
                return di;
            }

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                float critMask = v.vcol.r;
                float digitMask = v.vcol.g;
                float textMask = v.vcol.b;

                #if defined(DI_STRUCTURED_BUFFER) || defined(DI_CBUFFER_INSTANCING)

                float4 _Color = 0;
                float _Alpha = 0;
                bool _IsCrit = 0;
                bool _IsMiss = 0;
                bool _IsAgi = 0;
                bool _IsSlow = 0;
                bool _IsExp = 0;
                int _Value = 0;
                float _LifeTime = 0;
                float _CritJitter = 0;

                #if defined(DI_STRUCTURED_ACTIVE)
                #if UNITY_ANY_INSTANCING_ENABLED
                uint id = (uint)_BaseInstance + unity_InstanceID;
                #else
                uint id = (uint)_BaseInstance;
                #endif
                DamageIndicatorData d = _Instances[id];
                _IsCrit = IsCrit(d);
                _IsMiss = IsMiss(d);
                _IsAgi = IsAgi(d);
                _IsSlow = IsSlow(d);
                _IsExp = IsExp(d);
                _Alpha = d.alpha;
                _Value = d.value;
                _Color = d.color;
                _LifeTime = d.lifeTime;
                _CritJitter = d.critJitter;
                #elif defined(DI_CBUFFER_INSTANCING)
                float4 p0 = UNITY_ACCESS_INSTANCED_PROP(DamageIndicatorProps, _DIParams0);
                float4 p1 = UNITY_ACCESS_INSTANCED_PROP(DamageIndicatorProps, _DIParams1);
                _Color = UNITY_ACCESS_INSTANCED_PROP(DamageIndicatorProps, _DIColor);
                _Alpha = p0.x;
                _Value = (int)round(p0.y);
                _LifeTime = p0.z;
                _CritJitter = p0.w;
                uint flags = (uint)round(p1.x);
                _IsCrit = (flags & (1u << 0)) != 0u;
                _IsMiss = (flags & (1u << 1)) != 0u;
                _IsAgi  = (flags & (1u << 2)) != 0u;
                _IsSlow = (flags & (1u << 3)) != 0u;
                _IsExp  = (flags & (1u << 4)) != 0u;
                #endif

                #endif
                v.vertex *= _IsCrit ? 1 : !critMask;
                v.vertex *= _IsMiss | _IsAgi | _IsExp ? 1 : !textMask;
                v.vertex *= !(_IsMiss | _IsAgi/* | _IsExp*/) ? 1 : !digitMask;

                float offset = 0.1;
                int digitIndex = v.index * (1/offset);

                DamageIndicator di = GetDamageIndicator(_Value);

                switch (di.magnitude)
                {
                    default:
                    case 0:
                        v.uv.x += di.digits[digitIndex] * offset * digitMask;
                        break;
                    case 1:
                    case 2:
                        if (digitIndex == 4)
                        {
                            v.uv.y -= offset * 2;
                            v.uv.x += offset * (di.magnitude - 1);
                        }
                        else
                        {
                            v.uv.x += di.digits[digitIndex + 1] * offset * digitMask;
                        }
                        break;
                }

                const float digitSize = 0.5;
                const float digitHalf = digitSize * 0.5;
                const int totalDigits = 5;
                const float center[5] = {0,0.5,1.5,3,5};

                float spacing = _IsExp ? 0 : (1-_Spacing);
                v.vertex.x -= (spacing * (totalDigits - digitIndex) * digitSize * digitMask) - (digitSize * spacing * digitMask);
                v.vertex.x += spacing * digitMask * rcp(di.count) * center[di.count-1];
                v.vertex.x -= (di.count + min(di.magnitude, 1)) * digitHalf * digitMask;
                v.vertex.x += _CritJitter * critMask * 0.5;
                v.vertex.y += _IsCrit & !critMask ? -0.05 : 0;

                v.vertex.x -= _IsExp ? di.count * digitHalf * textMask + 0.5 * textMask : 0;
                v.vertex *= _IsExp ? 0.75 : 1;
                v.vertex.x += _IsExp ? di.count * digitHalf * 0.5 + 0.25 : 0;
                v.uv.y -= (_IsAgi ? 0.2 : _IsExp ? 0.4 : 0) * textMask;
                v.uv.x += (_IsSlow ? 0.2 : 0) * textMask;

                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = lerp(1, _Color, !critMask) * lerp(1, 0.8, _IsCrit * critMask);
                o.alpha = _Alpha;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(col.a - 0.5);

                col.rgb *= i.color.rgb;
                col.a = i.alpha;
                return col;
            }
            ENDHLSL
        }
    }
}

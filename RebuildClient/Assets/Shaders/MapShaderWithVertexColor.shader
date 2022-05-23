Shader "Custom/MapShaderWithVertexColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Specular ("Specular", Range(0,1)) = 0.0
//        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Cutoff("Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="AlphaTest" "RenderType" = "TransparentCutout"}
        LOD 200

        Blend One OneMinusSrcAlpha

        CGPROGRAM

        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf StandardSpecularHack fullforwardshadows alphatest:_Cutoff

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        half _Glossiness;
        half _Specular;
        fixed4 _Color;


        //from our globals
        float4 _RoAmbientColor;

        #include "UnityPBSLighting.cginc"

        float Remap(half value, half from1, half to1, half from2, half to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        half4 BRDF1_Unity_PBS_Hack(half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
            float3 normal, float3 viewDir, UnityLight light, UnityIndirect gi)
        {
            float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
            float3 halfDir = Unity_SafeNormalize(float3(light.dir) + viewDir);

            // NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
            // In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
            // but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
            // Following define allow to control this. Set it to 0 if ALU is critical on your platform.
            // This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
            // Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
            half shiftAmount = dot(normal, viewDir);
            normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
            // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
            //normal = normalize(normal);

            float nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
            half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

            float nl = saturate(dot(normal, light.dir));
            float nh = saturate(dot(normal, halfDir));

            half lv = saturate(dot(light.dir, viewDir));
            half lh = saturate(dot(light.dir, halfDir));

            // Diffuse term
            half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

            // Specular term
            // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
            // BUT 1) that will make shader look significantly darker than Legacy ones
            // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
            float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if UNITY_BRDF_GGX
            // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
            roughness = max(roughness, 0.002);
            float V = SmithJointGGXVisibilityTerm(nl, nv, roughness);
            float D = GGXTerm(nh, roughness);
#else
            // Legacy
            half V = SmithBeckmannVisibilityTerm(nl, nv, roughness);
            half D = NDFBlinnPhongNormalizedTerm(nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
#endif

            float specularTerm = V * D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

#   ifdef UNITY_COLORSPACE_GAMMA
            specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

            // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
            specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
            specularTerm = 0.0;
#endif

            // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
            half surfaceReduction;
#   ifdef UNITY_COLORSPACE_GAMMA
            surfaceReduction = 1.0 - 0.28 * roughness * perceptualRoughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
#   else
            surfaceReduction = 1.0 / (roughness * roughness + 1.0);           // fade \in [0.5;1]
#   endif

    // To provide true Lambert lighting, we need to be able to kill specular completely.
            specularTerm *= any(specColor) ? 1.0 : 0.0;

            half grazingTerm = saturate(smoothness + (1 - oneMinusReflectivity));
            half3 color = diffColor * (gi.diffuse + light.color * diffuseTerm)
                + specularTerm * light.color * FresnelTerm(specColor, lh)
                + surfaceReduction * gi.specular * FresnelLerp(specColor, grazingTerm, nv);

            //half3 d = _RoAmbientColor + gi.diffuse;


            //color = gi.diffuse * light.color * diffuseTerm; // lerp(d, +gi.diffuse / 2;
            //color = diffColor * gi.diffuse;
            ////color /= 2;
            //color *= light.color;

            //color += specularTerm * light.color * FresnelTerm(specColor, lh)
            //    + surfaceReduction * gi.specular * FresnelLerp(specColor, grazingTerm, nv);

            return half4(color, 1);
        }

        half4 LightingStandardSpecularHack(SurfaceOutputStandardSpecular s, float3 viewDir, UnityGI gi)
        {
            s.Normal = normalize(s.Normal);
            s.Albedo *= _RoAmbientColor;

            // energy conservation
            half oneMinusReflectivity;
            s.Albedo = EnergyConservationBetweenDiffuseAndSpecular(s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);

            // shader relies on pre-multiply alpha-blend (_SrcBlend = One, _DstBlend = OneMinusSrcAlpha)
            // this is necessary to handle transparency in physically correct way - only diffuse component gets affected by alpha
            half outputAlpha;
            s.Albedo = PreMultiplyAlpha(s.Albedo, s.Alpha, oneMinusReflectivity, /*out*/ outputAlpha);

            //if (outputAlpha > 0.9)
                //return half4(s.Albedo * Remap(gi.light.color, 0, 1, _RoAmbientColor, 1), outputAlpha);

            half4 c = BRDF1_Unity_PBS_Hack(s.Albedo, s.Specular, oneMinusReflectivity, s.Smoothness, s.Normal, viewDir, gi.light, gi.indirect);
            c.a = outputAlpha;
            return c;
        }

        inline half4 LightingStandardSpecularHack_Deferred(SurfaceOutputStandardSpecular s, float3 viewDir, UnityGI gi, out half4 outGBuffer0, out half4 outGBuffer1, out half4 outGBuffer2)
        {
            // energy conservation
            half oneMinusReflectivity;
            s.Albedo = EnergyConservationBetweenDiffuseAndSpecular(s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);

            half4 c = BRDF1_Unity_PBS_Hack(s.Albedo, s.Specular, oneMinusReflectivity, s.Smoothness, s.Normal, viewDir, gi.light, gi.indirect);

            UnityStandardData data;
            data.diffuseColor = s.Albedo;
            data.occlusion = s.Occlusion;
            data.specularColor = s.Specular;
            data.smoothness = s.Smoothness;
            data.normalWorld = s.Normal;

            UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

            half4 emission = half4(s.Emission + c.rgb, 1);
            return emission;
        }

        inline void LightingStandardSpecularHack_GI(
            SurfaceOutputStandardSpecular s,
            UnityGIInput data,
            inout UnityGI gi)
        {
#if defined(UNITY_PASS_DEFERRED) && UNITY_ENABLE_REFLECTION_BUFFERS
            gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal);
#else
            Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(s.Smoothness, data.worldViewDir, s.Normal, s.Specular);
            gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, g);
#endif
        }


        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandardSpecular o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color * IN.color;
            o.Albedo = c;
            // Metallic and smoothness come from slider variables
            o.Specular = _Specular;
            //o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
	FallBack "Transparent/Cutout/VertexLit"
}

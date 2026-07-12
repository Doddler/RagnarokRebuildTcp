Shader"Ragnarok/CharacterSpriteShader - Color"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _AtlasArray("Atlas Array", 2DArray) = "" {}
        [PerRendererData] _Color("Tint", Color) = (1,1,1,1)
        [PerRendererData] _EnvColor("Environment", Color) = (1,1,1,1)
        [PerRendererData] _Offset("Offset", Float) = 0
        [PerRendererData] _Width("Width", Float) = 0
        [PerRendererData] _VPos("VerticalPos", Float) = 0
        _ColorDrain("Color Drain", Range(0,1)) = 0
        _Rotation("Rotation", Range(0,360)) = 0
    }

    SubShader
    {
        Tags{"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"}

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "Color"
            Tags{"LightMode" = "UniversalForward"}
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ SMOOTHPIXEL
            #pragma multi_compile _ BLINDEFFECT_ON
            #pragma shader_feature _ COLOR_DRAIN

            #pragma multi_compile_local _ DYNBATCH_ON

            #include "SpriteColorOnlyPass.cginc"
            ENDHLSL
        }
    }
}

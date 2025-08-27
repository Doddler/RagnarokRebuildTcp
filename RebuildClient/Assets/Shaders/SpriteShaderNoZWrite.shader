Shader"Ragnarok/CharacterSpriteShader - Color"
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
        _ColorDrain("Color Drain", Range(0,1)) = 0
        _Rotation("Rotation", Range(0,360)) = 0
    }

    SubShader
    {

        Tags{"Queue" = "Transparent" "LightMode" = "Vertex" "IgnoreProjector" = "True" "RenderType" = "Transparent"}

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        
        Pass
        {
            Name "Color"
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            //#pragma multi_compile _ PIXELSNAP_ON
            //#pragma multi_compile _ PALETTE_ON
            #pragma multi_compile _ SMOOTHPIXEL
            #pragma multi_compile _ BLINDEFFECT_ON
            //#pragma shader_feature _ WATER_OFF
            #pragma shader_feature _ COLOR_DRAIN
            #pragma multi_compile _ GROUND_ITEM

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap
            #pragma multi_compile _ INSTANCING_ON
            
            //#define SMOOTHPIXEL

            #include "SpriteColorOnlyPass.cginc"
            ENDCG
        }
    }
}
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Effects
{
    //gravity color val lookup. Why in this file? Because I don't have a better spot for it.
    // 0 : white, OneMinusInverseAlpha
    // 1 : 255/175/175
    // 2 : 195/195/255 OneMinusInverseAlpha
    // 3 : white, alpha 80, OneMinusInverseAlpha
    // 4 : 100/100/255
    // 5 : white
    // 6 : 255/100/100
    // 7 : 100/255/100
    // 8 : 255/255/170
    // 9 : 140/210/140
    // 10 : 255/89/182
    // 11 : 170/170/255
    // 12 : 50/50/50 OneMinusInverseAlpha
    // 13 : 255/50/255 OneMinusInverseAlpha
    // 14 : 120/120/255 OneMinusInverseAlpha
    // 15 : 10/255/10
    
    public enum EffectMaterialType
    {
        SkillSpriteAlphaBlended,
        TeleportPillar,
        SafetyWall,
        EffectMaterialMax
    }
    
    public enum EffectTextureType
    {
        AlphaDown,
        MagicViolet,
        EffectTextureMax
    }
    
    public static class EffectSharedMaterialManager
    {
        private static Material[] materialList;
        private static bool[] persistMaterials;
        
        private static Texture2D[] textureList;
        private static bool[] persistTextures;

        private static SpriteAtlas skillAtlas;

        public static SpriteAtlas GetSkillSpriteAtlas()
        {
            if (skillAtlas == null)
                skillAtlas = Resources.Load<SpriteAtlas>("SkillAtlas");

            return skillAtlas;
        }

        public static Material GetMaterial(EffectMaterialType mat)
        {
            if (materialList == null)
            {
                materialList = new Material[(int)EffectMaterialType.EffectMaterialMax];
                persistMaterials = new bool[(int)EffectMaterialType.EffectMaterialMax];
                textureList = new Texture2D[(int)EffectTextureType.EffectTextureMax];
                persistTextures = new bool[(int)EffectTextureType.EffectTextureMax];
            }
            
            if (materialList[(int)mat] == null)
            {

                switch (mat)
                {
                    case EffectMaterialType.SkillSpriteAlphaBlended:
                        if (materialList[(int)mat] == null)
                        {
                            var atlas = GetSkillSpriteAtlas();
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendParticleShader)
                            {
                                color = Color.white,
                                mainTexture = atlas.GetSprite("FireBolt1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
                    
                    case EffectMaterialType.TeleportPillar:
                        if (textureList[(int)EffectTextureType.MagicViolet] == null)
                        {
                            textureList[(int)EffectTextureType.MagicViolet] = Resources.Load<Texture2D>("magic_violet");
                            persistTextures[(int)EffectTextureType.MagicViolet] = true;
                        }
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AdditiveShader)
                            {
                                mainTexture = Resources.Load<Texture2D>("magic_violet"),
                                color = new Color(100 / 255f, 100 / 255f, 255 / 255f),
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
                    
                    case EffectMaterialType.SafetyWall:
                        if (textureList[(int)EffectTextureType.AlphaDown] == null)
                        {
                            textureList[(int)EffectTextureType.AlphaDown] = Resources.Load<Texture2D>("alpha_down");
                            persistTextures[(int)EffectTextureType.AlphaDown] = true;
                        }
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AdditiveShader)
                            {
                                mainTexture = textureList[(int)EffectTextureType.AlphaDown],
                                color = new Color(255 / 255f, 89 / 255f, 182 / 255f),
                                renderQueue = 3001
                            };
                        }
                        break;
                }
            }


            return materialList[(int)mat];
        }

        public static void CleanUpMaterialsOnSceneChange()
        {
            if (materialList == null || textureList == null)
                return;
            
            for (var i = 0; i < (int)EffectMaterialType.EffectMaterialMax; i++)
            {
                if (materialList[i] != null && persistMaterials[i])
                    continue;

                GameObject.Destroy(materialList[i]);
                materialList[i] = null;
            }

            for (var i = 0; i < (int)EffectTextureType.EffectTextureMax; i++)
            {
                if (textureList[i] != null && persistTextures[i])
                    continue;

                GameObject.Destroy(textureList[i]);
                textureList[i] = null;
            }
        }
    }
}
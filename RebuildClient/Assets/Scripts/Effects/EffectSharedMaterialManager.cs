using System.Collections.Generic;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        SkillSpriteAlphaBlendedNoZCheck,
        TeleportPillar,
        SafetyWall,
        ParticleAlphaBlend,
        ParticleAdditive,
        IceMaterial,
        SightEffect,
        ShadowMaterial,
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
        private static SpriteAtlas particleAtlas;
        private static Dictionary<string, Material> projectileMaterials;
        private static Dictionary<string, RoSpriteData> projectileSprites = new();
        private static Dictionary<string, AsyncOperationHandle<RoSpriteData>> loadingSprites = new();

        public static void PrepareEffectSprite(string spriteName)
        {
            if (projectileSprites.ContainsKey(spriteName) || loadingSprites.ContainsKey(spriteName))
                return;
            
            var task = Addressables.LoadAssetAsync<RoSpriteData>(spriteName);
            loadingSprites.Add(spriteName, task);
        }

        public static bool TryGetEffectSprite(string spriteName, out RoSpriteData data)
        {
            data = null;
            if (projectileSprites.TryGetValue(spriteName, out data))
                return true;
            
            if (loadingSprites.TryGetValue(spriteName, out var load))
            {
                if (!load.IsDone)
                    return false;
                projectileSprites.Add(spriteName, load.Result);
                data = load.Result;
                return true;
            }

            return true;
        }
        
        public static Material GetProjectileMaterial(string sprite)
        {
            projectileMaterials ??= new Dictionary<string, Material>();
            
            if (projectileMaterials.TryGetValue(sprite, out var mat))
                return mat;
            
            mat = new Material(ShaderCache.Instance.AlphaBlendParticleShader)
            {
                color = Color.white,
                renderQueue = 3001
            };
            projectileMaterials.Add(sprite, mat);
            return mat;
        }
        
        public static SpriteAtlas GetSkillSpriteAtlas()
        {
            if (skillAtlas == null)
                skillAtlas = Resources.Load<SpriteAtlas>("SkillAtlas");

            return skillAtlas;
        }
        
        
        public static SpriteAtlas GetParticleSpriteAtlas()
        {
            if (particleAtlas == null)
                particleAtlas = Resources.Load<SpriteAtlas>("Particles");

            return particleAtlas;
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
                    case EffectMaterialType.ParticleAlphaBlend:
                        if (materialList[(int)mat] == null)
                        {
                            var atlas = GetParticleSpriteAtlas();
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendParticleShader)
                            {
                                color = Color.white,
                                mainTexture = atlas.GetSprite("particle1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
                    case EffectMaterialType.ParticleAdditive:
                        if (materialList[(int)mat] == null)
                        {
                            var atlas = GetParticleSpriteAtlas();
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AdditiveShader)
                            {
                                color = Color.white,
                                mainTexture = atlas.GetSprite("particle1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
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
                    case EffectMaterialType.SkillSpriteAlphaBlendedNoZCheck:
                        if (materialList[(int)mat] == null)
                        {
                            var atlas = GetSkillSpriteAtlas();
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader)
                            {
                                color = Color.white,
                                mainTexture = atlas.GetSprite("FireBolt1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3003
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
                    case EffectMaterialType.IceMaterial:
                        if (materialList[(int)mat] == null)
                        {
                            var atlas = GetParticleSpriteAtlas();
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendParticleShader)
                            {
                                color = Color.white,
                                mainTexture = Resources.Load<Texture2D>("ice"),
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = false;
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
                                mainTexture = textureList[(int)EffectTextureType.MagicViolet],
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
                    case EffectMaterialType.SightEffect:
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendParticleShader)
                            {
                                color = Color.white,
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = false;
                        }
                        break;
                    case EffectMaterialType.ShadowMaterial:
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.SpriteShaderNoZWriteAlt)
                            {
                                color = Color.white,
                                renderQueue = 2999,
                                mainTexture = ClientDataLoader.Instance.ShadowSprite.texture
                            };
                            materialList[(int)mat].SetFloat("_Offset", 0.4f);
                            persistMaterials[(int)mat] = false;
                        }
                        break;
                }
            }


            return materialList[(int)mat];
        }

        public static void CleanUpMaterialsOnSceneChange()
        {
            if (projectileMaterials != null)
            {
                foreach(var m in projectileMaterials)
                    GameObject.Destroy(m.Value);
                projectileMaterials.Clear();
            }
            projectileSprites.Clear();
            
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
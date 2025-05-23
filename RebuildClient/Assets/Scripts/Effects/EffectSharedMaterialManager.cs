using System;
using System.Collections.Generic;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
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
        SkillSpriteAlphaAdditive,
        SkillSpriteAlphaAdditiveNoZCheck,
        SkillSpriteAlphaBlended,
        SkillSpriteAlphaBlendedNoZCheck,
        TeleportPillar,
        SafetyWall,
        ParticleAlphaBlend,
        ParticleAdditive,
        IceMaterial,
        FireRing,
        SightEffect,
        WaterBallEffect,
        ShadowMaterial,
        CastFire,
        CastWater,
        CastWind,
        CastEarth,
        MagnumBreak,
        EffectMaterialMax
    }

    public enum EffectTextureType
    {
        AlphaDown,
        MagicViolet,
        MagicGreen,
        RingRed,
        RingBlue,
        RingYellow,
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
        private static readonly int Offset = Shader.PropertyToID("_Offset");

        public static void PrepareEffectSprite(string spriteName)
        {
            if (projectileSprites.ContainsKey(spriteName) || loadingSprites.ContainsKey(spriteName))
                return;

            try
            {
                var task = Addressables.LoadAssetAsync<RoSpriteData>(spriteName);
                loadingSprites.Add(spriteName, task);
            }
            catch (Exception)
            {
                Debug.LogError($"Could not find addressable with the name: {spriteName}");
            }

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

        private static SpriteAtlas spriteAtlas;

        public static SpriteAtlas SpriteAtlas
        {
            get
            {
                if (spriteAtlas == null)
                    spriteAtlas = Resources.Load<SpriteAtlas>("SkillAtlas");
                return spriteAtlas;
            }
        }

        public static SpriteAtlas GetParticleSpriteAtlas()
        {
            if (particleAtlas == null)
                particleAtlas = Resources.Load<SpriteAtlas>("Particles");

            return particleAtlas;
        }

        private static Texture2D GetOrLoadEffectTexture(EffectTextureType type, bool persistTexture = true)
        {
            if (textureList[(int)type] != null)
                return textureList[(int)type];

            var texName = type switch
            {
                EffectTextureType.RingBlue => "ring_blue",
                EffectTextureType.RingRed => "ring_red",
                EffectTextureType.MagicGreen => "magic_green",
                EffectTextureType.RingYellow => "ring_yellow",
                EffectTextureType.AlphaDown => "alpha_down",
                EffectTextureType.MagicViolet => "magic_violet",
                _ => null
            };
            if (texName == null)
            {
                Debug.LogWarning($"Could not GetOrLoadEffectTexture type {type}");
                return null;
            }

            if (textureList[(int)type] == null)
            {
                textureList[(int)type] = Resources.Load<Texture2D>(texName);
                persistTextures[(int)type] = persistTexture;
            }

            return textureList[(int)type];
        }

        private static void SetUpTextureMaterial(EffectMaterialType type, Shader shader, Texture2D tex, int renderQueue = 3001, bool persist = false) =>
            SetUpTextureMaterial(type, shader, tex, Color.white, renderQueue, persist);

        private static void SetUpTextureMaterial(EffectMaterialType type, Shader shader, Texture2D tex, Color color, int renderQueue = 3001,
            bool persist = false)
        {
            if (materialList[(int)type] == null)
            {
                var mat = new Material(shader)
                {
                    color = color,
                    renderQueue = renderQueue
                };

                // if (!string.IsNullOrWhiteSpace(tex))
                //     mat.mainTexture = Resources.Load<Texture2D>(tex);
                mat.mainTexture = tex;

                materialList[(int)type] = mat;
                persistMaterials[(int)type] = persist;
            }
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
                    
                    case EffectMaterialType.SkillSpriteAlphaAdditive:
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AdditiveShader)
                            {
                                color = Color.white,
                                mainTexture = SpriteAtlas.GetSprite("FireBolt1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
                    case EffectMaterialType.SkillSpriteAlphaAdditiveNoZCheck:
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AdditiveShaderNoZTest)
                            {
                                color = Color.white,
                                mainTexture = SpriteAtlas.GetSprite("FireBolt1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3003
                            };
                            persistMaterials[(int)mat] = true;
                        }
                        break;
                    case EffectMaterialType.SkillSpriteAlphaBlended:
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendParticleShader)
                            {
                                color = Color.white,
                                mainTexture = SpriteAtlas.GetSprite("FireBolt1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3001
                            };
                            persistMaterials[(int)mat] = true;
                        }

                        break;
                    case EffectMaterialType.SkillSpriteAlphaBlendedNoZCheck:
                        if (materialList[(int)mat] == null)
                        {
                            materialList[(int)mat] = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader)
                            {
                                color = Color.white,
                                mainTexture = SpriteAtlas.GetSprite("FireBolt1").texture, //this will work unless the atlas gets split across multiple textures
                                renderQueue = 3003
                            };
                            persistMaterials[(int)mat] = true;
                        }

                        break;
                    case EffectMaterialType.IceMaterial:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AlphaBlendParticleShader, Resources.Load<Texture2D>("ice"));
                        break;
                    case EffectMaterialType.FireRing:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.PerspectiveAlphaShader, GetOrLoadEffectTexture(EffectTextureType.RingYellow));
                        break;
                    case EffectMaterialType.TeleportPillar:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AdditiveShader, GetOrLoadEffectTexture(EffectTextureType.MagicViolet),
                            new Color(100 / 255f, 100 / 255f, 255 / 255f), 3001, true);
                        break;
                    case EffectMaterialType.SafetyWall:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AdditiveShader, GetOrLoadEffectTexture(EffectTextureType.AlphaDown),
                            new Color(255 / 255f, 89 / 255f, 182 / 255f));
                        break;
                    case EffectMaterialType.SightEffect:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AlphaBlendParticleShader, null);
                        break;
                    case EffectMaterialType.WaterBallEffect:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AlphaBlendParticleShader, null);
                        break;
                    case EffectMaterialType.ShadowMaterial:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.SpriteShaderNoZWriteAlt, ClientDataLoader.Instance.ShadowSprite.texture, 2999);
                        materialList[(int)mat].SetFloat(Offset, 0.4f);
                        break;
                    case EffectMaterialType.CastFire:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AdditiveShader, GetOrLoadEffectTexture(EffectTextureType.RingRed), 3001, true);
                        break;
                    case EffectMaterialType.CastWater:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.InvAlphaShader, GetOrLoadEffectTexture(EffectTextureType.RingBlue),
                            new Color(170 / 255f, 170 / 255f, 255 / 255f), 3001, true);
                        break;
                    case EffectMaterialType.CastWind:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AdditiveShader, GetOrLoadEffectTexture(EffectTextureType.RingYellow), 3001, true);
                        break;
                    case EffectMaterialType.CastEarth:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.InvAlphaShader, GetOrLoadEffectTexture(EffectTextureType.MagicGreen), 3001, true);
                        break;
                    case EffectMaterialType.MagnumBreak:
                        SetUpTextureMaterial(mat, ShaderCache.Instance.AlphaBlendParticleShader, Resources.Load<Texture2D>("BigBang"));
                        break;
                }
            }

            return materialList[(int)mat];
        }

        public static void CleanUpMaterialsOnSceneChange()
        {
            if (projectileMaterials != null)
            {
                foreach (var m in projectileMaterials)
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
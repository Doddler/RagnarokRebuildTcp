using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.MapEditor.Editor;
using Assets.Scripts.Objects;
using RebuildSharedData.ClientTypes;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Editor
{
    public static class EffectStrImporter
    {
        [MenuItem("Ragnarok/Import and Update Skill Effect Atlas")]
        public static void ImportEffectTextures()
        {
            
            var atlasPath = "Assets/Textures/Resources/SkillAtlas.spriteatlasv2";

            if (!File.Exists(atlasPath))
                TextureImportHelper.CreateAtlas("SkillAtlas.spriteatlasv2", "Assets/Textures/Resources/");

            var atlas = SpriteAtlasAsset.Load(atlasPath);
            var atlasObj = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            
            atlas.Remove(atlasObj.GetPackables());
            
            var sprites = new List<Sprite>();
            sprites.Add(ImportEffectTexture("texture/effect/불화살1.tga", "FireBolt1"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살2.tga", "FireBolt2"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살3.tga", "FireBolt3"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살4.tga", "FireBolt4"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살5.tga", "FireBolt5"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살6.tga", "FireBolt6"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살7.tga", "FireBolt7"));
            sprites.Add(ImportEffectTexture("texture/effect/불화살8.tga", "FireBolt8"));
            sprites.Add(ImportEffectTexture("texture/effect/coin_a.bmp", "coin_a"));
            sprites.Add(ImportEffectTexture("texture/effect/coin_b.bmp", "coin_b"));
            sprites.Add(ImportEffectTexture("texture/effect/coin_c.bmp", "coin_c"));
            sprites.Add(ImportEffectTexture("texture/effect/coin_d.bmp", "coin_d"));
            sprites.Add(ImportEffectTexture("texture/effect/coin_e.bmp", "coin_e"));
            sprites.Add(ImportEffectTexture("texture/effect/icearrow.tga", "icearrow"));
            sprites.Add(ImportEffectTexture("texture/effect/lens1.tga", "lens1"));
            sprites.Add(ImportEffectTexture("texture/effect/lens2.tga", "lens2"));
            sprites.Add(ImportEffectTexture("texture/effect/super1.bmp", "super1"));
            sprites.Add(ImportEffectTexture("texture/effect/super2.bmp", "super2"));
            sprites.Add(ImportEffectTexture("texture/effect/super3.bmp", "super3"));
            sprites.Add(ImportEffectTexture("texture/effect/super4.bmp", "super4"));
            sprites.Add(ImportEffectTexture("texture/effect/super5.bmp", "super5"));
            

            atlas.Add(sprites.ToArray());

            EditorUtility.SetDirty(atlasObj);
            
            SpriteAtlasAsset.Save(atlas, atlasPath);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            var importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
            var settings = importer.packingSettings;
            settings.enableTightPacking = false;
            settings.enableRotation = false;
            importer.packingSettings = settings;
            EditorUtility.SetDirty(importer);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            SpriteAtlasUtility.PackAtlases(new[] {atlasObj}, BuildTarget.StandaloneWindows64);
        }

        private static Sprite ImportEffectTexture(string texName, string outName = "", bool keepExisting = false)
        {
            if (string.IsNullOrWhiteSpace(outName))
                outName = texName;
            var srcPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, texName);
            var fName = Path.GetFileNameWithoutExtension(outName);
            var destPath = Path.Combine($"Assets/Textures/Import/{fName}.png");

            if (keepExisting && File.Exists(destPath))
                return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
            
            var tex = TextureImportHelper.LoadTexture(srcPath);
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(destPath, bytes);
            
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            //
            // TextureImportHelper.SaveAndUpdateTexture(tex, destPath, ti =>
            // {
            //     ti.textureType = TextureImporterType.Sprite;
            //     ti.spriteImportMode = SpriteImportMode.Single;
            //     ti.textureCompression = TextureImporterCompression.Uncompressed;
            // });

            return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
        }

        [MenuItem("Ragnarok/Load Effects")]
        public static void Import()
        {
            //ImportEffectTextures();
            
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/effects.json");
            var effectList = JsonUtility.FromJson<EffectTypeList>(asset.text).Effects;

            if (!Directory.Exists("Assets/Effects/Prefabs/"))
                Directory.CreateDirectory("Assets/Effects/Prefabs/");

            //effectList = new List<EffectTypeEntry>();

            // foreach(var fname in Directory.GetFiles(@"G:\Projects2\Ragnarok\Resources\data\texture\effect", "*.str"))
            //     effectList.Add(new EffectTypeEntry() {StrFile = Path.GetFileNameWithoutExtension(fname), Name = Path.GetFileNameWithoutExtension(fname), ImportEffect = true});
            //     

            foreach (var e in effectList)
            {
                if (!e.ImportEffect)
                    continue;

                var prefabPath = $"Assets/Effects/Prefabs/{e.Name}.prefab";
                
                if (File.Exists(prefabPath))
                    continue;


                if (!string.IsNullOrWhiteSpace(e.StrFile))
                {
                    try
                    {
                        var loader = new RagnarokEffectLoader();
                        var anim = loader.Load(Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, @$"texture\effect\{e.StrFile}.str"), e.Name);
                        if (anim == null)
                            continue;

                        loader.MakeAtlas(@"Assets/Effects/Atlas/");

                        var obj = new GameObject(e.Name);
                        var renderer = obj.AddComponent<RoEffectRenderer>();
                        var sorter = obj.AddComponent<SortingGroup>();
                        //var billboard = obj.AddComponent<Billboard>();

                        if (!string.IsNullOrWhiteSpace(e.SoundFile))
                        {
                            var assetPath = $"Assets/Sounds/{e.SoundFile}.ogg";
                            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

                            if (clip == null)
                            {
                                assetPath = $"Assets/Sounds/Effects/{e.SoundFile}.ogg";
                                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                            }

                            if (clip != null)
                            {
                                var audio = obj.AddComponent<EffectAudioSource>();
                                audio.Volume = 1f;
                                audio.Clip = clip;
                                renderer.AudioSource = audio;
                            }
                            else
                            {
                                Debug.LogWarning("Could not load sound file at : " + assetPath);
                            }
                        }

                        renderer.Anim = anim;

                        PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
                        Object.DestroyImmediate(obj);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }

                if (!string.IsNullOrWhiteSpace(e.Sprite))
                {
                    var obj = new GameObject(e.Name);
                    var effect = obj.AddComponent<SpriteEffect>();

                    var spritePath = "Assets/Sprites/Effects/";
                    var sprite = AssetDatabase.LoadAssetAtPath<RoSpriteData>(spritePath + e.Sprite + ".spr");

                    effect.SpriteData = sprite;
                    effect.IsLoop = true;

                    PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(obj);
                }
            }

            RagnarokMapImporterWindow.UpdateAddressables();
        }
    }
}
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
        [MenuItem("Ragnarok/Import and Update Skill Effect Atlas", false, 125)]
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
            sprites.Add(ImportEffectTexture("texture/effect/ac_center2.tga", "ac_center2"));
            sprites.Add(ImportEffectTexture("texture/effect/endure.tga", "endure"));
            sprites.Add(ImportEffectTexture("texture/effect/pok1.tga", "pok1"));
            sprites.Add(ImportEffectTexture("texture/effect/pok3.tga", "pok3"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_ball_a.bmp", "thunder_ball_a"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_ball_b.bmp", "thunder_ball_b"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_ball_c.bmp", "thunder_ball_c"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_ball_d.bmp", "thunder_ball_d"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_ball_e.bmp", "thunder_ball_e"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_ball_f.bmp", "thunder_ball_f"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_center.bmp", "thunder_center"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_pang.bmp", "thunder_pang"));
            sprites.Add(ImportEffectTexture("texture/effect/black_sword.bmp", "black_sword"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_plazma_blast_a.bmp", "thunder_plazma_blast_a"));
            sprites.Add(ImportEffectTexture("texture/effect/thunder_plazma_blast_b.bmp", "thunder_plazma_blast_b"));
            
            ImportEffectTexture("texture/effect/대폭발.tga", "BigBang", false, "Resources");
            ImportEffectTexture("texture/effect/stone.bmp", "stone", false, "Resources");

            // ImportEffectTexture("texture/유저인터페이스/disable_card_slot.bmp", "disable_card_slot");
            // ImportEffectTexture("texture/유저인터페이스/empty_card_slot.bmp", "empty_card_slot");

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

        private static Sprite ImportEffectTexture(string texName, string outName = "", bool keepExisting = false, string dir = "Import")
        {
            if (string.IsNullOrWhiteSpace(outName))
                outName = texName;
            var srcPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, texName);
            var fName = Path.GetFileNameWithoutExtension(outName);
            var destPath = Path.Combine($"Assets/Textures/{dir}/{fName}.png");

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
                {
                    Debug.Log($"Skipping import of {prefabPath} as it already exists.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(e.StrFile))
                {
                    try
                    {
                        Debug.Log($"Importing str animation {e.StrFile}");
                        
                        var loader = new RagnarokEffectLoader();
                        var importPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, @$"texture\effect\{e.StrFile}.str");
                        var anim = loader.Load(importPath, e.Name);
                        if (anim == null)
                        {
                            Debug.Log($"Could not load [{importPath}] as the file was not found.");
                            continue;
                        }

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
                            
                            if (clip == null)
                            {
                                assetPath = $"Assets/Sounds/{e.SoundFile}.wav";
                                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                            }
                            
                            if (clip == null)
                            {
                                assetPath = $"Assets/Sounds/Effects/{e.SoundFile}.wav";
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
                        renderer.IsLoop = e.IsLooping;
                        renderer.RandomStart = e.IsLooping;
                        
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

                    var spritePath = "Assets/Sprites/Imported/Effects/";
                    var sprite = AssetDatabase.LoadAssetAtPath<RoSpriteData>(spritePath + e.Sprite + ".asset");

                    effect.SpriteData = sprite;
                    effect.IsLoop = true;

                    PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(obj);
                }
            }

            RagnarokMapImporterWindow.UpdateAddressables(false);
        }
    }
}
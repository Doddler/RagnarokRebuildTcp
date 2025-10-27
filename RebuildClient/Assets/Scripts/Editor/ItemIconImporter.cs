﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts.MapEditor.Editor;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Editor
{
    public static class ItemIconImporter
    {
        private static Texture2D BlowUpTexture(Texture2D src)
        {
            var tex = new Texture2D(src.width * 2, src.height * 2, src.format, false);
            for (var x = 0; x < src.width; x++)
            {
                for (var y = 0; y < src.height; y++)
                {
                    var c = src.GetPixel(x, y);
                    tex.SetPixels(x * 2, y * 2, 2, 2, new[] { c, c, c, c });
                }
            }

            return tex;
        }

        [MenuItem("Ragnarok/Import Skill and Item Icons", false, 124)]
        public static void ImportItems()
        {
            var iconNames = new List<string>();
            var convertName = new Dictionary<string, string>();
            var skills = JsonUtility.FromJson<Wrapper<SkillData>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/skillinfo.json"));

            foreach (var skill in skills.Items) { 
                if (!string.IsNullOrWhiteSpace(skill.Icon) && !iconNames.Contains(skill.Icon))
                {
                    iconNames.Add(skill.Icon);
                    convertName.Add(skill.Icon, "skill_" + skill.Icon);
                }
            }

            var items = JsonUtility.FromJson<Wrapper<ItemData>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/items.json"));
            var sharedItemSprites = new StringBuilder();
            var equipIcons = new List<string>();
            var itemIdToCode = new Dictionary<int, string>();

            foreach (var item in items.Items)
            {
                itemIdToCode.Add(item.Id, item.Code);
                if (!string.IsNullOrWhiteSpace(item.Sprite) && !iconNames.Contains(item.Code))
                {
                    if (iconNames.Contains(item.Sprite))
                    {
                        sharedItemSprites.AppendLine($"{item.Code}\t{convertName[item.Sprite]}");
                        continue;
                    }

                    iconNames.Add(item.Sprite);
                    convertName.Add(item.Sprite, item.Code);
                    if (item.IsUnique)
                        equipIcons.Add(item.Sprite);
                }
            }

            //do the things

            File.WriteAllText("Assets/Data/SharedItemIcons.txt", sharedItemSprites.ToString());

            var atlasPath = "Assets/Textures/ItemAtlas.spriteatlasv2";
            if (!File.Exists(atlasPath))
                TextureImportHelper.CreateAtlas("ItemAtlas.spriteatlasv2", "Assets/Textures/");

            var atlas = SpriteAtlasAsset.Load(atlasPath);
            var atlasObj = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);


            atlas.Remove(atlasObj.GetPackables());
            var sprites = new List<Sprite>();

            var srcPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "sprite/아이템");
            var statusPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "texture/effect");
            var collectionSrcPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "texture/유저인터페이스/collection");
            var collectionAltSrcPath = "Assets/Textures/CustomIcons/Collection";
            var cardIllustSrcPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "texture/유저인터페이스/cardbmp");
            Debug.Log(srcPath);

            var i = 0;
            foreach (var icon in iconNames)
            {
                i++;

                var fName = icon;
                var newName = convertName[icon];
                // if (newName == "Amulet")
                //     Debug.Log(icon);
                var destPath = $@"Assets/Sprites/Imported/Icons/Sprites/{newName}.png";
                var collectionDestPath = $"Assets/Sprites/Imported/Collections/{newName}.png";
                var importedAssetName = $"Assets/Sprites/Imported/Icons/{fName}.asset";
                // Debug.Log(destPath);

                if (!Directory.Exists("Assets/Sprites/Icons/"))
                    Directory.CreateDirectory("Assets/Sprites/Icons/");


                if (!Directory.Exists("Assets/Sprites/Imported/Collections/"))
                    Directory.CreateDirectory("Assets/Sprites/Imported/Collections/");

                if (!File.Exists(collectionDestPath))
                {
                    var collectionSrc = Path.Combine(collectionSrcPath, $"{fName}.bmp");
                    if (File.Exists(collectionSrc))
                    {
                        var tex = TextureImportHelper.LoadTexture(collectionSrc);
                        TextureImportHelper.SaveAndUpdateTexture(tex, collectionDestPath, ti =>
                        {
                            ti.textureType = TextureImporterType.Sprite;
                            ti.spriteImportMode = SpriteImportMode.Single;
                            ti.textureCompression = TextureImporterCompression.CompressedHQ;
                            ti.crunchedCompression = true;
                        });
                    }
                    else
                    {
                        collectionSrc = Path.Combine(collectionAltSrcPath, $"{fName}.bmp");
                        // Debug.Log(collectionSrc);
                        if (File.Exists(collectionSrc))
                        {
                            var tex = TextureImportHelper.LoadTexture(collectionSrc);
                            TextureImportHelper.SaveAndUpdateTexture(tex, collectionDestPath, ti =>
                            {
                                ti.textureType = TextureImporterType.Sprite;
                                ti.spriteImportMode = SpriteImportMode.Single;
                                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                                ti.crunchedCompression = true;
                            });
                        }
                        // else
                        //     Debug.Log("Not found :( " + collectionSrc);
                    }
                }

                if (!File.Exists(destPath))
                {
                    var iconPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "texture/유저인터페이스/item", fName + ".bmp");
                    if (!File.Exists(iconPath))
                    {
                        iconPath = Path.Combine("Assets/Textures/CustomIcons/Item", fName + ".bmp");
                        if (!File.Exists(iconPath))
                        {
                            iconPath = Path.Combine("Assets/Textures/CustomIcons/Skills", fName + ".bmp");
                            if (!File.Exists(iconPath))
                            {
                                Debug.LogWarning($"Could not find spr file with name {iconPath}");
                                continue;
                            }
                        }
                    }

                    var tex = TextureImportHelper.LoadTexture(iconPath);
                    var offset = new Vector2(tex.width / 2f, tex.height / 2f);
                    var pivot = new Vector2(0.5f, 0.5f);
                    var lowerBounds = 0;
                    if (equipIcons.Contains(icon))
                    {
                        for (var y = 0; y < tex.height / 2f; y++)
                        {
                            for (var x = 0; x < tex.width; x++)
                            {
                                if (tex.GetPixel(x, y).a > 0)
                                {
                                    lowerBounds = y;
                                    break;
                                }
                            }

                            if (lowerBounds > 0)
                                break;
                        }

                        offset = new Vector2(tex.width / 2f, lowerBounds + 5f);
                        pivot = offset / new Vector2(tex.width, tex.height);
                    }

                    TextureImportHelper.SaveAndUpdateTexture(tex, destPath, ti =>
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.textureCompression = TextureImporterCompression.Uncompressed;
                        ti.spritePivot = offset;

                        var settings = new TextureImporterSettings();
                        ti.ReadTextureSettings(settings);
                        settings.spriteAlignment = (int)SpriteAlignment.Custom;
                        settings.spritePivot = pivot;
                        ti.SetTextureSettings(settings);
                    }, false);

                    // var sprPath = Path.Combine(srcPath, fName + ".spr");
                    // var actPath = Path.Combine(srcPath, fName + ".act");
                    // if (!File.Exists(sprPath) || !File.Exists(actPath))
                    // {
                    //     Debug.LogWarning($"Could not find spr file with name {sprPath}");
                    //     continue;
                    // }
                    //
                    // var newSprPath = $"Assets/Sprites/Icons/{fName}.spr";
                    // var newActPath = $"Assets/Sprites/Icons/{fName}.act";
                    //
                    // if(!File.Exists(newSprPath))
                    //     File.Copy(sprPath, newSprPath);
                    // if(!File.Exists(newActPath))
                    //     File.Copy(actPath, newActPath);
                    //
                    // AssetDatabase.ImportAsset(newSprPath, ImportAssetOptions.ForceUpdate);
                    // AssetDatabase.Refresh();
                    //
                    // var spriteData = AssetDatabase.LoadAssetAtPath<RoSpriteData>(importedAssetName);
                    // var curIcon = spriteData.Sprites[0];
                    // var offset = spriteData.Actions[0].Frames[0].Layers[0].Position;
                    //
                    // //this is stupid, but if we load the texture asset normally it might not be readable
                    // var iconAtlas = new Texture2D(2, 2);
                    // iconAtlas.LoadImage(File.ReadAllBytes($"Assets/Sprites/Imported/Icons/Atlas/{spriteData.Atlas.name}.png"));
                    //
                    // var bounds = curIcon.rect;
                    // bounds = new Rect(offset.x, offset.y, curIcon.rect.width, curIcon.rect.height);
                    //
                    // var newTex = new Texture2D((int)bounds.width, (int)bounds.height, TextureFormat.ARGB32, false);
                    //
                    // Graphics.CopyTexture(iconAtlas, 0, 0, (int)curIcon.textureRect.xMin, (int)curIcon.textureRect.yMin, 
                    //     (int)curIcon.rect.width, (int)curIcon.rect.height, newTex, 0, 0, 0, 0);
                    // //newTex.SetPixels(24, 24, texture.width, texture.height, texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false));
                    //
                    // //
                    // // var bytes = texture.EncodeToPNG();
                    // // File.WriteAllBytes(destPath, bytes);
                    // //
                    // TextureImportHelper.SaveAndUpdateTexture(newTex, destPath, ti =>
                    // {
                    //     ti.textureType = TextureImporterType.Sprite;
                    //     ti.spriteImportMode = SpriteImportMode.Single;
                    //     ti.textureCompression = TextureImporterCompression.Uncompressed;
                    //     ti.spritePivot = offset;
                    //     
                    //     var settings = new TextureImporterSettings();
                    //     ti.ReadTextureSettings(settings);
                    //     settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    //     settings.spritePivot = new Vector2(0.5f + (offset.x/curIcon.rect.width/2f), 0.5f + (offset.y/curIcon.rect.height/2f));
                    //     ti.SetTextureSettings(settings);
                    // });
                    //
                    // AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
                    // AssetDatabase.Refresh();
                    //
                    // GameObject.DestroyImmediate(iconAtlas);
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);

                // Debug.Log(sprite);
                // var texOut = TextureImportHelper.SaveAndUpdateTexture(texture, );

                if (sprite == null)
                    Debug.LogWarning($"Unexpectedly we do not have a sprite! Expected to load {destPath}");

                if (!sprites.Contains(sprite))
                    sprites.Add(sprite);
            }

            var statusData = JsonUtility.FromJson<Wrapper<StatusEffectData>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/statusinfo.json"));

            foreach (var status in statusData.Items)
                if (!string.IsNullOrWhiteSpace(status.Icon) && !iconNames.Contains(status.Icon))
                {
                    var targetName = status.StatusEffect.ToString();
                    var destPath = $@"Assets/Sprites/Imported/Icons/Sprites/status_{targetName}.png";
                    var statusSrc = Path.Combine(statusPath, $"{status.Icon}.tga");
                    var psdPath = $@"Assets/Textures/CustomIcons/{status.Icon}.psd";

                    if (File.Exists(psdPath)) //always copy if there's an override status icon
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(psdPath);
                        TextureImportHelper.SaveAndUpdateTexture(tex, destPath, ti =>
                        {
                            ti.textureType = TextureImporterType.Sprite;
                            ti.spriteImportMode = SpriteImportMode.Single;
                            ti.textureCompression = TextureImporterCompression.Uncompressed;
                        }, false);
                    }
                    else if (!File.Exists(destPath) && File.Exists(statusSrc))
                    {
                        var tex = TextureImportHelper.LoadTexture(statusSrc);
                        TextureImportHelper.SaveAndUpdateTexture(tex, destPath, ti =>
                        {
                            ti.textureType = TextureImporterType.Sprite;
                            ti.spriteImportMode = SpriteImportMode.Single;
                            ti.textureCompression = TextureImporterCompression.Uncompressed;
                        }, false);
                    }

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
                    if (sprite == null)
                    {
                        Debug.LogWarning($"Could not find status effect icon: {statusSrc}");
                        continue;
                    }

                    if (!sprites.Contains(sprite))
                        sprites.Add(sprite);
                }


            Debug.Log($"Imported {i} item/skill icon sprites!");

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
            var atlasSettings = importer.textureSettings;
            atlasSettings.filterMode = FilterMode.Bilinear; // FilterMode.Point;
            importer.textureSettings = atlasSettings;
            EditorUtility.SetDirty(importer);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SpriteAtlasUtility.PackAtlases(new[]
            {
                atlasObj
            }, BuildTarget.StandaloneWindows64);


            //card illustrations
            var cardIllustData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/cardillustrations.txt");
            var cardIllustLines = cardIllustData.text.Split('\n');

            foreach (var line in cardIllustLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var s = line.Split('#');
                if (s.Length < 2 || !int.TryParse(s[0], out var id))
                    continue;

                if (!itemIdToCode.TryGetValue(id, out var itemCode))
                    continue;

                var collectionDestPath = $"Assets/Sprites/Imported/Collections/cardart_{itemCode}.png";

                if (!File.Exists(collectionDestPath))
                {
                    var collectionSrc = Path.Combine(cardIllustSrcPath, $"{s[1]}.bmp");
                    if (File.Exists(collectionSrc))
                    {
                        var tex = TextureImportHelper.LoadTexture(collectionSrc);
                        TextureImportHelper.SaveAndUpdateTexture(tex, collectionDestPath, ti =>
                        {
                            ti.textureType = TextureImporterType.Sprite;
                            ti.spriteImportMode = SpriteImportMode.Single;
                            ti.textureCompression = TextureImporterCompression.CompressedHQ;
                            ti.crunchedCompression = true;
                        });
                    }
                }
            }

            var defaultCardIllustration = $"Assets/Sprites/Imported/Collections/cardart_default.png";
            if (!File.Exists(defaultCardIllustration))
            {
                var defaultCardIllustPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "texture/유저인터페이스/cardbmp/sorry.bmp");
                if (File.Exists(defaultCardIllustPath))
                {
                    var tex = TextureImportHelper.LoadTexture(defaultCardIllustPath);
                    TextureImportHelper.SaveAndUpdateTexture(tex, defaultCardIllustration, ti =>
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.textureCompression = TextureImporterCompression.CompressedHQ;
                        ti.crunchedCompression = true;
                    });
                }
            }
        }
    }
}
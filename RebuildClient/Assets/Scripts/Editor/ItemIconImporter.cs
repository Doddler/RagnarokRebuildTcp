using System.Collections.Generic;
using System.IO;
using Assets.Editor;
using Assets.Scripts.MapEditor.Editor;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using UnityEditor;
using UnityEditor.AssetImporters;
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
                    tex.SetPixels(x*2, y*2, 2, 2, new []{c, c, c, c});
                }
            }

            return tex;
        }
        
        [MenuItem("Ragnarok/Import Skill and Item Icons")]
        public static void ImportItems()
        {
            var iconNames = new List<string>();
            var convertName = new Dictionary<string, string>();
            var skillDataFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/skillinfo.json");
            var skills = JsonUtility.FromJson<Wrapper<SkillData>>(skillDataFile.text);
            foreach (var skill in skills.Items)
                if (!string.IsNullOrWhiteSpace(skill.Icon) && !iconNames.Contains(skill.Icon))
                {
                    iconNames.Add(skill.Icon);
                    convertName.Add(skill.Icon, "skill_" + skill.Icon);
                }
            
            var itemDataFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/items.json");
            var items = JsonUtility.FromJson<Wrapper<ItemData>>(itemDataFile.text);
            
            foreach(var item in items.Items)
                if (!string.IsNullOrWhiteSpace(item.Sprite) && !iconNames.Contains(item.Code))
                {
                    if (iconNames.Contains(item.Sprite))
                        continue;
                    iconNames.Add(item.Sprite);
                    convertName.Add(item.Sprite, item.Code);
                }

            var atlasPath = "Assets/Textures/ItemAtlas.spriteatlasv2";
            if (!File.Exists(atlasPath))
                TextureImportHelper.CreateAtlas("ItemAtlas.spriteatlasv2", "Assets/Textures/");
            
            var atlas = SpriteAtlasAsset.Load(atlasPath);
            var atlasObj = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            
            
            atlas.Remove(atlasObj.GetPackables());
            var sprites = new List<Sprite>();

            var srcPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "sprite/아이템");
            Debug.Log(srcPath);

            var i = 0;
            foreach (var icon in iconNames)
            {
                i++;

                var fName = icon;
                var newName = convertName[icon];
                if(fName != icon)
                    Debug.Log(icon);
                var destPath = $@"Assets/Sprites/Imported/Icons/Sprites/{newName}.png";
                var importedAssetName = $"Assets/Sprites/Imported/Icons/{fName}.asset";
                // Debug.Log(destPath);

                if (!File.Exists(destPath))
                {
                    var sprPath = Path.Combine(srcPath, fName + ".spr");
                    var actPath = Path.Combine(srcPath, fName + ".act");
                    if (!File.Exists(sprPath) || !File.Exists(actPath))
                    {
                        Debug.LogWarning($"Could not find spr file with name {sprPath}");
                        continue;
                    }

                    var newSprPath = $"Assets/Sprites/Icons/{fName}.spr";
                    var newActPath = $"Assets/Sprites/Icons/{fName}.act";
                    
                    if(!File.Exists(newSprPath))
                        File.Copy(sprPath, newSprPath);
                    if(!File.Exists(newActPath))
                        File.Copy(actPath, newActPath);
                    
                    AssetDatabase.ImportAsset(actPath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();
                    
                    var spriteData = AssetDatabase.LoadAssetAtPath<RoSpriteData>(importedAssetName);
                    var iconAtlas = spriteData.Atlas;
                    var curIcon = spriteData.Sprites[0];
                    var offset = spriteData.Actions[0].Frames[0].Layers[0].Position;

                    var bounds = curIcon.rect;
                    bounds = new Rect(offset.x, offset.y, curIcon.rect.width, curIcon.rect.height);
                    
                    var newTex = new Texture2D((int)bounds.width, (int)bounds.height, TextureFormat.ARGB32, false);

                    Graphics.CopyTexture(iconAtlas, 0, 0, (int)curIcon.textureRect.xMin, (int)curIcon.textureRect.yMin, 
                        (int)curIcon.rect.width, (int)curIcon.rect.height, newTex, 0, 0, 0, 0);
                    //newTex.SetPixels(24, 24, texture.width, texture.height, texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false));
                
                    //
                    // var bytes = texture.EncodeToPNG();
                    // File.WriteAllBytes(destPath, bytes);
                    //
                    TextureImportHelper.SaveAndUpdateTexture(newTex, destPath, ti =>
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.textureCompression = TextureImporterCompression.Uncompressed;
                        ti.spritePivot = offset;
                        
                        var settings = new TextureImporterSettings();
                        ti.ReadTextureSettings(settings);
                        settings.spriteAlignment = (int)SpriteAlignment.Custom;
                        settings.spritePivot = new Vector2(0.5f + (offset.x/curIcon.rect.width/2f), 0.5f + (offset.y/curIcon.rect.height/2f));
                        ti.SetTextureSettings(settings);
                    });

                    AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);

                // Debug.Log(sprite);
                // var texOut = TextureImportHelper.SaveAndUpdateTexture(texture, );
            
                if(sprite == null)
                    Debug.LogWarning($"Unexpectedly we do not have a sprite! Expected to load {destPath}");
                
                if(!sprites.Contains(sprite))
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
            
            SpriteAtlasUtility.PackAtlases(new[] {atlasObj}, BuildTarget.StandaloneWindows64);
            
        }

    }
}
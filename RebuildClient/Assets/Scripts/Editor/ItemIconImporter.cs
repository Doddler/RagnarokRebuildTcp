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
            var skillDataFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/skillinfo.json");
            var skills = JsonUtility.FromJson<Wrapper<SkillData>>(skillDataFile.text);
            foreach (var skill in skills.Items)
                if(!string.IsNullOrWhiteSpace(skill.Icon) && !iconNames.Contains(skill.Icon))
                    iconNames.Add(skill.Icon);
            
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
                var destPath = $@"Assets/Sprites/Icons/{fName}.png";
                // Debug.Log(destPath);

                if (!File.Exists(destPath))
                {
                    var sprPath = Path.Combine(srcPath, fName + ".spr");
                    if (!File.Exists(sprPath))
                    {
                        Debug.LogWarning($"Could not find spr file with name {sprPath}");
                        continue;
                    }

                    var sprImporter = new RagnarokSpriteLoader();
                    var texture = sprImporter.LoadFirstSpriteTextureOnly(Path.Combine(srcPath, fName + ".spr"));
                    // texture = BlowUpTexture(texture);
                    texture.alphaIsTransparency = true;
                    
                    //
                    // var bytes = texture.EncodeToPNG();
                    // File.WriteAllBytes(destPath, bytes);
                    //
                    TextureImportHelper.SaveAndUpdateTexture(texture, destPath, ti =>
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.textureCompression = TextureImporterCompression.Uncompressed;
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
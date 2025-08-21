using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Editor;
using Assets.Scripts;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "act", AllowCaching = true)]
public class ActImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        //ImportActFile(ctx.assetPath);
    }

    public static void ImportActFile(string actFilePath, string assetDirectoryPath = null)
    {
        //Check for .spr pair immediately, otherwise quit the import
        string sprFilePath = actFilePath.Replace(".act", ".spr");
        if (!File.Exists(sprFilePath))
        {
            throw new NotSupportedException($"Couldn't find a matching {Path.GetFileName(sprFilePath)} file, aborting import");
        };
        
        string actFileName = Path.GetFileNameWithoutExtension(actFilePath);
        string actDirectoryPath = Path.GetDirectoryName(actFilePath) ?? throw new ArgumentNullException($"Resulting diretory for {actFilePath} cannot be null");
        string atlasFilePath = Path.Combine(assetDirectoryPath ?? actDirectoryPath, "Atlas", $"{actFileName}_atlas.png");
        string palettePath = Path.Combine(actDirectoryPath, "Palette");
        var palettes = new List<string>();
    
        for (var i = 0; i < 10; i++)
        {
            var pName = Path.Combine(palettePath, $"{actFileName}_{i}_1.pal");
            if (File.Exists(pName))
                palettes.Add(pName);
            pName = Path.Combine(palettePath, $"{actFileName}_{i}.pal");
            if (File.Exists(pName))
                palettes.Add(pName);
        }

        var spriteAsset = ScriptableObject.CreateInstance<RoSpriteData>();
        AssetDatabase.CreateAsset(spriteAsset, Path.Combine(assetDirectoryPath ?? actDirectoryPath, $"{actFileName}.asset"));

        var loader = new RagnarokSpriteLoader();
        loader.Load(actFilePath.Replace(".act", ".spr"), atlasFilePath, spriteAsset, null);
        SetUpSpriteData(loader, spriteAsset, actDirectoryPath, actFileName, actFileName);

        for (var i = 0; i < palettes.Count; i++)
        {
            spriteAsset = ScriptableObject.CreateInstance<RoSpriteData>();
            AssetDatabase.CreateAsset(spriteAsset, Path.Combine(assetDirectoryPath ?? actDirectoryPath, $"{actFileName}_{i}.asset"));

            atlasFilePath = Path.Combine(assetDirectoryPath ?? actDirectoryPath, "Atlas", $"{actFileName}_{i}_atlas.png").Replace("\\", "/");
            loader = new RagnarokSpriteLoader();
            loader.Load(actFilePath.Replace(".act", ".spr"), atlasFilePath, spriteAsset, palettes[i]);
            SetUpSpriteData(loader, spriteAsset, actDirectoryPath, actFileName, $"{actFileName}_{i}");
        }
        AssetDatabase.SaveAssets();
    }

    private static void SetUpSpriteData(RagnarokSpriteLoader spr, RoSpriteData asset, string basePath, string baseName, string outName)
    {
        var actName = Path.Combine(basePath, baseName + ".act");
        var imfFile = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectorySafe, "imf", baseName + ".imf");
        if (!File.Exists(imfFile))
            imfFile = null;
        
        var actLoader = new RagnarokActLoader();
        var actions = actLoader.Load(spr, actName, imfFile);

        asset.Actions = actions.ToArray();
        asset.Sprites = spr.Sprites.ToArray();
        asset.SpriteSizes = spr.SpriteSizes.ToArray();
        asset.Name = outName;
        asset.Atlas = spr.Atlas;
        asset.SpritesPerPalette = spr.SpriteFrameCount;

        if (actLoader.Sounds != null)
        {
            asset.Sounds = new AudioClip[actLoader.Sounds.Length];

            for (var i = 0; i < asset.Sounds.Length; i++)
            {
                var soundAction = actLoader.Sounds[i];
                if (soundAction == "atk")
                    continue;
                var sPath = $"Assets/Sounds/{soundAction}";

                var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(sPath);
                if (!sound)
                    throw new FileNotFoundException($"Sound {sPath} for sprite {baseName} not found.");
                asset.Sounds[i] = sound;
            }
        }

        asset.Type = asset.Actions.Length switch
        {
            8 or 13 => SpriteType.Npc,
            32 => SpriteType.ActionNpc,
            39 or 40 or 41 => SpriteType.Monster, //zerom/mi gao/increase soil for some reason
            47 or 48 => SpriteType.Monster2, //dullahan for some reason
            56 or 64 => SpriteType.Monster,  //56 ???
            72 => SpriteType.Pet,
            _ => asset.Type
        };

        var maxExtent = 0f;
        var totalWidth = 0f;
        var widthCount = 0;

        foreach (var action in asset.Actions)
        {
            var frameId = 0;
            foreach (var frame in action.Frames)
            {
                if (frame.IsAttackFrame)
                    asset.AttackFrameTime = action.Delay * frameId;

                frameId++;

                foreach (var layer in frame.Layers)
                {
                    if (layer.Index == -1)
                        continue;
                    var sprite = asset.SpriteSizes[layer.Index];
                    var y = layer.Position.y + sprite.y / 2f;
                    if (layer.Position.x < 0)
                        y = Mathf.Abs(layer.Position.y - sprite.y / 2f);
                    if (y > maxExtent)
                        maxExtent = y;
                    totalWidth += Mathf.Abs(layer.Position.x) + sprite.x / 2f;
                    widthCount++;
                }
            }
        }

        //far better way to get sprite attack timing
        if (asset.Type is (SpriteType.Monster or SpriteType.Monster2 or SpriteType.Pet))
        {
            var actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack1);
            if (actionId == -1)
                actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack2);
            if (actionId >= 0)
            {
                var frames = asset.Actions[actionId].Frames;
                var found = false;

                for (var j = 0; j < frames.Length; j++)
                {
                    if (frames[j].IsAttackFrame)
                    {
                        asset.AttackFrameTime = j * asset.Actions[actionId].Delay;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var pos = frames.Length - 2;
                    if (pos < 0)
                        pos = 0;
                    asset.AttackFrameTime = pos * asset.Actions[actionId].Delay;
                }
            }
        }

        asset.Size = Mathf.CeilToInt(maxExtent);
        asset.AverageWidth = totalWidth / widthCount;
        
        //ok so this is a giant shitshow. We want to know where to display emotes, cast bars, and npcs above this character
        //to do so, we save the highest y value of the first frame of the first action.
        //a lot of monsters use a high overhead attack which we don't want to count, so using the idle pose is probably safer
        asset.StandingHeight = 20;
        try
        {
            var firstFrame = asset.Actions[0].Frames[0];
            var maxHeight = 
            (
                from layer in firstFrame.Layers where layer.Index >= 0 
                let curSpr = asset.Sprites[layer.Index] 
                select curSpr.rect.height / 2 - layer.Position.y
            ).Prepend(0f).Max();

            if (maxHeight > asset.StandingHeight)
                asset.StandingHeight = maxHeight;
        }
        catch (Exception)
        {
            throw new Exception($"Couldn't process standing height for sprite {asset.Name}");
        }
    }
}

public class ActPostProcessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths,
        bool didDomainReload)
    {
        foreach (var importedAsset in importedAssets)
        {
            if (!importedAsset.EndsWith(".act"))
                continue;
            
            var sprName = Path.Combine(Path.GetDirectoryName(importedAsset), Path.GetFileNameWithoutExtension(importedAsset) + ".spr");
            if (!File.Exists(sprName))
            {
                Debug.LogError($"Could not load sprite {importedAsset} as it did not have an associated .spr sprite data file.");
                continue;
            }

            ActImporter.ImportActFile(importedAsset);
        }
    }
}
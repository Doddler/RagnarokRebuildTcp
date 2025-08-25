using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Editor;
using Assets.Scripts;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEngine;
using static Assets.Scripts.MapEditor.Editor.RagnarokDirectory.ActDirectoriesID;

public class GrfDataFilesCategorized
{
    public sealed class RoFilesList : List<string>
    {
        public uint Imported
        {
            get;
            set;
        }
    }
    
    public RoFilesList SoundFiles = new();
    public RoFilesList MonsterFiles = new();
    public RoFilesList HeadFiles = new();
    public RoFilesList BodyFiles = new();
    public RoFilesList HeadgearFiles = new();
    public RoFilesList WeaponFiles = new();
    public RoFilesList ShieldFiles = new();
    public RoFilesList NpcFiles = new();
    public RoFilesList PaletteFiles = new();
    public RoFilesList CutinFiles = new();
    public RoFilesList MiscFiles = new();

    public string this[int index] => categoriesDict.Keys.ToArray()[index];
    public readonly RoFilesList[] Categories;
    public int Count => Categories.Aggregate(0, (acc, cur) => acc + cur.Count);
    private readonly Dictionary<string, RoFilesList> categoriesDict;
    
    public GrfDataFilesCategorized()
    {
        Categories = new[]
        {
            SoundFiles, MonsterFiles, HeadFiles,
            BodyFiles, HeadgearFiles, WeaponFiles,
            ShieldFiles, NpcFiles, PaletteFiles,
            CutinFiles, MiscFiles
        };
        
        categoriesDict = new Dictionary<string, RoFilesList>
        {
            ["SoundFiles"] = SoundFiles,
            ["MonsterFiles"] = MonsterFiles,
            ["HeadFiles"] = HeadFiles,
            ["BodyFiles"] = BodyFiles,
            ["HeadgearFiles"] = HeadgearFiles,
            ["WeaponFiles"] = WeaponFiles,
            ["ShieldFiles"] = ShieldFiles,
            ["NpcFiles"] = NpcFiles,
            ["PaletteFiles"] = PaletteFiles,
            ["CutinFiles"] = CutinFiles,
            ["MiscFiles"] = MiscFiles
        };
    }
}

/// <summary>
/// Prepares all relevant data from an extracted GRF to be used by the Rebuild client
/// </summary>
public static class RoDataImportManager // Open to suggestions on this name
{
    private static string TranslateFilename(string name)
    {
        return name
            .Replace("성직자_", "Acolyte_")
            .Replace("궁수_", "Archer_")
            .Replace("마법사_", "Mage_")
            .Replace("상인_", "Merchant_")
            .Replace("초보자_", "Novice_")
            .Replace("검사_", "Swordsman_")
            .Replace("도둑_", "Thief_")
            .Replace("여_", "F_")
            .Replace("남_", "M_");
    }
    
    /// <summary>
    /// Goes through all RagnarokDirectory content and categorize file paths
    /// </summary>
    /// <param name="inputDataPath"></param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static GrfDataFilesCategorized ReadGrfDataFolder(string inputDataPath = null)
    {
        inputDataPath = ValidateGrfDataFolder(inputDataPath);
        Debug.Log("Reading and categorizing Grf Data folder");

        var grfData = new GrfDataFilesCategorized();
        
        var directoryToCategoryMap = new Dictionary<int, GrfDataFilesCategorized.RoFilesList>()
        {
            {(int)ActMonstersDirectories, grfData.MonsterFiles},
            {(int)ActPlayerHeadDirectories, grfData.HeadFiles},
            {(int)ActPlayerBodyDirectories, grfData.BodyFiles},
            {(int)ActPlayerHeadgearDirectories, grfData.HeadgearFiles},
            {(int)ActWeaponsDirectories, grfData.WeaponFiles},
            {(int)ActShieldsDirectories, grfData.ShieldFiles},
            {(int)ActNpcDirectories, grfData.NpcFiles}
        };
        
        foreach (var filePath in Directory.EnumerateFiles(inputDataPath, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(filePath);
            var fileRelativePath = Path.GetRelativePath(inputDataPath, filePath);
            var directoryRelativePath =  Path.GetDirectoryName(fileRelativePath);
            switch (extension)
            {
                case ".wav" or ".ogg":
                    grfData.SoundFiles.Add(filePath);
                    if (WasThisFileAlreadyImported(filePath))
                        grfData.SoundFiles.Imported++;
                    break;
                case ".act":
                    foreach (var (directoriesCategory, id) in RagnarokDirectory.ActDirectories.Select((directories, id) => (directory: directories, id)))
                    {
                        if (directoriesCategory.Contains(directoryRelativePath))
                        {
                            directoryToCategoryMap[id]
                                .Add(filePath);
                            if (WasThisFileAlreadyImported(filePath))
                                directoryToCategoryMap[id].Imported++;
                            break;
                        }
                        if (RagnarokDirectory.ExpectedMiscFiles.Contains(fileRelativePath))
                        {
                            grfData.MiscFiles.Add(filePath);
                            // if (WasThisFileAlreadyImported(filePath))
                            //     grfData.IncrementImportedCount(nameof(grfData.MiscFiles));
                            break;
                        }
                    }
                    break;
                case ".pal":
                    grfData.PaletteFiles.Add(filePath);
                    if (WasThisFileAlreadyImported(filePath))
                        grfData.PaletteFiles.Imported++;
                    break;
                case ".bmp":
                    if (RagnarokDirectory.ImgCutinDirectories.Contains(directoryRelativePath))
                    {
                        grfData.CutinFiles.Add(filePath);
                        if (WasThisFileAlreadyImported(filePath))
                            grfData.CutinFiles.Imported++;
                    }
                    break;
            }
        }
        return grfData;
    }
    public static void ProcessGrfData(string inputDataPath = null)
    {
        var grfData = ReadGrfDataFolder(inputDataPath);


        Debug.Log("Sorted files by type");
        Debug.Log($"Processing grf data with {grfData.Count} Files");
        ProcessWavFiles(grfData.SoundFiles);
        ProcessActFiles(grfData.MonsterFiles);
        //ProcessPalFiles(palFilePaths); // TODO: Rework spr/act so that sprites are kept in a indexed format and use the color lookup table to render the correct colors 
        //ProcessRswFiles(rswFilePaths);
        Debug.Log("Done processing");
    }

    public static void ProcessGrfData(GrfDataFilesCategorized grfData)
    {
        Debug.Log($"Processing grf data with {grfData.Count} Files");
        Debug.Log($"{grfData.SoundFiles.Count} in Sounds");
        Debug.Log($"{grfData.MonsterFiles.Count} in Monsters");
        Debug.Log($"{grfData.HeadFiles.Count} in Heads");
        Debug.Log($"{grfData.BodyFiles.Count} in Body");
        Debug.Log($"{grfData.HeadgearFiles.Count} in Headgear");
        Debug.Log($"{grfData.WeaponFiles.Count} in Weapons");
        Debug.Log($"{grfData.ShieldFiles.Count} in Shields");
        Debug.Log($"{grfData.NpcFiles.Count} in Npcs");
        Debug.Log($"{grfData.PaletteFiles.Count} in Palettes");
        Debug.Log($"{grfData.CutinFiles.Count} in Cutins");
        Debug.Log($"{grfData.MiscFiles.Count} in Misc");
        
        ProcessWavFiles(grfData.SoundFiles);
        ProcessActFiles(grfData.MonsterFiles);
    }

    private static string ValidateGrfDataFolder(string inputDataPath = null)
    {
        Debug.Log($"Our input path is :{inputDataPath}");
        if (inputDataPath == null)
        {
            try
            {
                inputDataPath = RagnarokDirectory.GetRagnarokDataDirectory;
            }
            catch (DirectoryNotFoundException)
            {
                throw new DirectoryNotFoundException($"ProcessGrfData: directory not found: {inputDataPath}\nYou must pass a directory to ProcessGrfData or set a ragnarok data directory first!");
            }
        } else if (!Directory.Exists(inputDataPath))
            throw new DirectoryNotFoundException($"ProcessGrfData: directory not found: {inputDataPath}");
        
        return inputDataPath;
    }
    
    private static void ProcessWavFiles(List<string> wavFilesPath)
    {
        Debug.Log("Starting wav processing");
        if (wavFilesPath.Count == 0)
        {
            Debug.Log("No wav files to process");
            return;
        }
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var wavFilePath in wavFilesPath)
            {
                var wavFileName = Path.GetFileName(wavFilePath);
                var grfRelativeFolderPath = Path.GetRelativePath(RagnarokDirectory.GetRagnarokDataDirectory, Path.GetDirectoryName(wavFilePath));
                string assetRelativeFolderPath;
                try
                {
                    assetRelativeFolderPath = RagnarokDirectory.RelativeDirectoryConversion[grfRelativeFolderPath];
                }
                catch (KeyNotFoundException)
                {
                    Debug.Log($"Converted path not found for {grfRelativeFolderPath}. Skipping it.");
                    continue;
                }
                Directory.CreateDirectory(Path.Combine(Application.dataPath, assetRelativeFolderPath));
                File.Copy(wavFilePath, Path.Combine(Application.dataPath, assetRelativeFolderPath, wavFileName));
            }
        }
        catch (Exception)
        {
            Debug.LogError($"Issue during sound import stopping further imports.");
            throw;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
    }
    private static void ProcessRswFiles()
    {
        throw new NotImplementedException();
    }
    private static void ProcessPalFiles()
    {
        throw new NotImplementedException();
    }
    private static void ProcessActFiles(List<string> actFilePaths, bool overwrite = false)
    {
        Debug.Log("Starting act processing");
        if (actFilePaths.Count == 0)
        {
            Debug.Log("No act files to process");
            return;
        }
        Debug.Log($"We have the following list: {actFilePaths}");
        
        foreach (var actFilePath in actFilePaths)
        {
            //Debug.Log($"Got an act file:{actFilePath}");
            var sprFilePath = DoesSprExist(actFilePath, true);
            if (sprFilePath == null) // Skipping this .act
                continue;
            var imfFilePath = DoesImfExist(actFilePath);
            
            var fileName = Path.GetFileNameWithoutExtension(actFilePath);
            var grfRelativeFolderPath = Path.GetRelativePath(RagnarokDirectory.GetRagnarokDataDirectory, Path.GetDirectoryName(actFilePath));
            string assetRelativeFolderPath;
            try
            {
                assetRelativeFolderPath = RagnarokDirectory.RelativeDirectoryConversion[grfRelativeFolderPath];
            }
            catch (KeyNotFoundException)
            {
                Debug.Log($"Converted path not found for {grfRelativeFolderPath}. Skipping it.");
                continue;
            }
            
            if (File.Exists(Path.Combine("Assets", assetRelativeFolderPath, fileName, ".asset")) && !overwrite)
                continue;
            
            var roSpriteData = ScriptableObject.CreateInstance<RoSpriteData>();
            Directory.CreateDirectory(Path.Combine("Assets", assetRelativeFolderPath));
            
            // TODO: Rethink texture loading so that we don't need to do both a CreateAsset and a SaveAssets after the texture import
            var assetAtlasFilePath = Path.Combine("Assets", assetRelativeFolderPath, "Atlas", $"{fileName}_atlas.png");
            AssetDatabase.CreateAsset(roSpriteData, Path.Combine("Assets", assetRelativeFolderPath, $"{fileName}.asset"));
            
            var loader = new RagnarokSpriteLoader();
            loader.Load(sprFilePath, assetAtlasFilePath, roSpriteData, null, imfFilePath);
            SetUpSpriteData(loader, roSpriteData, Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, grfRelativeFolderPath), fileName);
        }
        AssetDatabase.SaveAssets();
        
        // TODO: investigate the possibility of a batch import, but seems unlikely to be feasible
        // try
        // {
        //     AssetDatabase.StartAssetEditing();
        //     
        // }
        // catch (Exception)
        // {
        //     Debug.LogError("Issue during asset creation");
        //     throw;
        // }
        // finally
        // {
        //     
        //     AssetDatabase.StopAssetEditing();
        // }
    }

    private static bool WasThisFileAlreadyImported(string filePath)
    {
        var relativeFilePath =  Path.GetRelativePath(RagnarokDirectory.GetRagnarokDataDirectory, filePath);
        var relativeFolderPath = Path.GetDirectoryName(relativeFilePath);
        string relativeAssetFolderPath;
        try
        {
            relativeAssetFolderPath = RagnarokDirectory.RelativeDirectoryConversion[relativeFolderPath!];
        }
        catch (KeyNotFoundException e)
        {
            Debug.LogException(e);
            return false;
        }
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var fileExtension = Path.GetExtension(filePath);
        var relativeAssetFilePath = Path.Combine(relativeAssetFolderPath, fileName) + fileExtension;
        //Debug.Log($"Checking if file {filePath} was already imported as {relativeAssetFilePath} ");
        return fileExtension switch
        {
            ".act" => File.Exists(Path.Combine(Application.dataPath, relativeAssetFolderPath, fileName, ".asset")),
            _ => File.Exists(Path.Combine(Application.dataPath, relativeAssetFilePath))
        };
    }
    
    /// <summary>
    /// Check if a .spr file pair exists for the .act file on <paramref name="actFilePath"/>.<br/>
    /// Will ask for the .spr file path if <paramref name="autoSkip"/> is false
    /// </summary>
    /// <param name="actFilePath">Full .act file path</param>
    /// <param name="autoSkip">Skip import if no matching .spr file is found</param>
    /// <returns>Path to .spr file if or null if it was skipped</returns>
    private static string DoesSprExist(string actFilePath, bool autoSkip = false)
    {
        string sprFilePath = Path.ChangeExtension(actFilePath, ".spr");
        if (File.Exists(sprFilePath))
        {
            return sprFilePath;
        }
        if (!autoSkip)
        {
            if (EditorUtility.DisplayDialog("Sprite Data Missing", $"Matching .spr file not found for { Path.GetFileName(actFilePath) }.\n" +
                                                                   $"Please select the corresponding .spr file.", "Select", "Skip"))
            {
                sprFilePath = EditorUtility.OpenFilePanel("Select matching .spr", "", "spr");
                return sprFilePath;
            }
        }
        Debug.LogWarning($"Skipping {actFilePath} conversion.");
        return null;
    }
    
    private static string DoesImfExist(string actFilePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(actFilePath);
        var imfFilePath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectorySafe, "imf", fileName + ".imf");
        if (File.Exists(imfFilePath))
            return imfFilePath;
        Debug.LogWarning($"No IMF file for {actFilePath}");
        return null;
    }
    
    public static void ConvertToAsset(string actFilePath, string assetDirectoryPath = null)
    {
        //Check for .spr pair immediately, otherwise quit the import
        string sprFilePath = DoesSprExist(actFilePath);
        if (sprFilePath == null) return;
        Debug.Log($"Ready for conversion: {Path.GetFileName(actFilePath)} + {Path.GetFileName(sprFilePath)}");
        var roSpriteData = ScriptableObject.CreateInstance<RoSpriteData>();
    }
    
    public static void ImportActFile(string actFilePath, string assetDirectoryPath = null)
    {
        //Check for .spr pair immediately, otherwise quit the import
        string sprFilePath = actFilePath.Replace(".act", ".spr");
        if (!File.Exists(sprFilePath))
        {
            throw new NotSupportedException($"Couldn't find a matching {Path.GetFileName(sprFilePath)} file on path {sprFilePath}, aborting import");
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

    private static void SetUpSpriteData(RagnarokSpriteLoader spr, RoSpriteData asset, string basePath, string baseName, string outName = null)
    {
        outName ??= baseName;
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
                if (!Path.GetExtension(soundAction).Equals(".wav", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                var sPath = $"Assets/Sounds/{soundAction}";

                var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(sPath);
                if (!sound)
                {
                    Debug.LogError($"Couldn't find sound asset at {sPath} for sprite {baseName}. Skipping.");
                    continue;
                    //throw new FileNotFoundException($"Sound {sPath} for sprite {baseName} not found.");
                }
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

// public class ActPostProcessor : AssetPostprocessor
// {
//     private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths,
//         bool didDomainReload)
//     {
//         foreach (var importedAsset in importedAssets)
//         {
//             if (!importedAsset.EndsWith(".act"))
//                 continue;
//             
//             var sprName = Path.Combine(Path.GetDirectoryName(importedAsset), Path.GetFileNameWithoutExtension(importedAsset) + ".spr");
//             if (!File.Exists(sprName))
//             {
//                 Debug.LogError($"Could not load sprite {importedAsset} as it did not have an associated .spr sprite data file.");
//                 continue;
//             }
//
//             RoDataImportManager.ImportActFile(importedAsset);
//         }
//     }
// }
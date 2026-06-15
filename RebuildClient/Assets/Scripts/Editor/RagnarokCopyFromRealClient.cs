using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Editor;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    public class RagnarokCopyFromRealClient : EditorWindow
    {
        public static void TestCopy()
        {
            var dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;

            Func<string, string> updateHeadName = (str) => str.Replace("머리", "");
            Func<string, string> updateBodyName = (str) => str.Replace("몸", "");

            var headPath = Path.Combine(dataDir, "palette/머리");
            var bodyPath = Path.Combine(dataDir, "palette/몸");

            if (Directory.Exists(headPath))
            {
                CopyFolder(headPath, "Assets/Sprites/Characters/HeadFemale/Palette/", false, false, "*_여_*.pal",
                    updateHeadName);
                CopyFolder(headPath, "Assets/Sprites/Characters/HeadMale/Palette/", false, false, "*_남_*.pal",
                    updateHeadName);
            }

            headPath = Path.Combine(dataDir, "palette/머리/costume_1");
            bodyPath = Path.Combine(dataDir, "palette/몸/costume_1");

            if (Directory.Exists(bodyPath))
            {
                CopyFolder(bodyPath, "Assets/Sprites/Characters/BodyFemale/Palette/", false, false, "*_여_*.pal",
                    updateHeadName);
                CopyFolder(bodyPath, "Assets/Sprites/Characters/BodyMale/Palette/", false, false, "*_남_*.pal");
            }
        }

        private static string UpdateSpriteName(string name)
        {
            name = name.Replace("성직자_", "Acolyte_");
            name = name.Replace("궁수_", "Archer_");
            name = name.Replace("마법사_", "Mage_");
            name = name.Replace("상인_", "Merchant_");
            name = name.Replace("초보자_", "Novice_");
            name = name.Replace("검사_", "Swordsman_");
            name = name.Replace("도둑_", "Thief_");
            name = name.Replace("여_", "F_");
            name = name.Replace("남_", "M_");

            return name;
        }


        [MenuItem("Ragnarok/TestCopy")]
        public static void TestCopy2()
        {
            var dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/성직자"), "Assets/Sprites/Weapons/Acolyte/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/궁수"), "Assets/Sprites/Weapons/Archer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/마법사"), "Assets/Sprites/Weapons/Mage/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/상인"), "Assets/Sprites/Weapons/Merchant/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/초보자"), "Assets/Sprites/Weapons/Novice/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/검사"), "Assets/Sprites/Weapons/Swordsman/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/도둑"), "Assets/Sprites/Weapons/Thief/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/슈퍼노비스"), "Assets/Sprites/Weapons/SuperNovice/", false, true,
                "*", UpdateSpriteName);


            CopyFolder(Path.Combine(dataDir, "sprite/방패/성직자"), "Assets/Sprites/Shields/Acolyte/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/궁수"), "Assets/Sprites/Shields/Archer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/마법사"), "Assets/Sprites/Shields/Mage/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/상인"), "Assets/Sprites/Shields/Merchant/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/초보자"), "Assets/Sprites/Shields/Novice/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/검사"), "Assets/Sprites/Shields/Swordsman/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/도둑"), "Assets/Sprites/Shields/Thief/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/슈퍼노비스"), "Assets/Sprites/Shields/SuperNovice/", false, true,
                "*", UpdateSpriteName);
        }

        [MenuItem("Ragnarok/Copy data from client data folder", priority = 1)]
        public static void CopyClientData()
        {
            var dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;

            if (dataDir == null)
            {
                var prompt =
                    @"Before you continue, you will need to specify a directory containing the contents of an extracted data.grf. "
                    + "For this import process to work correctly, the files will need to have been extracted with the right locale and working korean file names.";

                if (!EditorUtility.DisplayDialog("Copy from RO Client", prompt, "Continue", "Cancel"))
                    return;

                RagnarokDirectory.SetDataDirectory();

                dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
                if (string.IsNullOrWhiteSpace(dataDir))
                    return;
            }

            bool TestPath(string fileName)
            {
                if (!File.Exists(Path.Combine(dataDir, fileName)))
                {
                    Debug.LogError(
                        $"Could not verify client data directory \"{dataDir}\" is valid. File checked: {fileName} ");
                    return false;
                }

                return true;
            }

            if (!TestPath("prontera.gat") || !TestPath(@"texture\워터\water000.jpg"))
                return;

            var prompt2 = @"This import process will copy files from your data folder into this project. "
                          + "Because this includes converting all maps and objects, expect this process to take more than an hour."
                          + "\n\nWhen complete, the lighting window will load where you can bake the lighting for all the scenes (accessible via 'Ragnarok->Lighting Manager'). "
                          + "You will also need to manually copy over your BGM into the music folder if you want music."
                          + "\n\nLastly, before you run you will need to use 'Ragnarok->Update Addressables' to make sure everything can load.";

            if (!EditorUtility.DisplayDialog("Copy from RO Client", prompt2, "Continue", "Cancel"))
                return;

            // Renaming Function from TestCopy function
            Func<string, string> updateHeadName = (str) => str.Replace("머리", "");
            Func<string, string> updateBodyName = (str) => str.Replace("몸", "");

            CopyFolder(Path.Combine(dataDir, "wav/"), "Assets/Sounds/", true);
            CopyFolder(Path.Combine(dataDir, "sprite/몬스터"), "Assets/Sprites/Monsters/");
            CopyFolder(Path.Combine(dataDir, "sprite/악세사리/남"), "Assets/Sprites/Headgear/Male/");
            CopyFolder(Path.Combine(dataDir, "sprite/악세사리/여"), "Assets/Sprites/Headgear/Female/");
            CopyFolder(Path.Combine(dataDir, "sprite/npc"), "Assets/Sprites/Npcs/");
            CopyFolder(Path.Combine(dataDir, "sprite/이팩트"), "Assets/Sprites/Effects/");
            CopyFolder(Path.Combine(dataDir, "palette/머리"), "Assets/Sprites/Characters/HeadFemale/Palette/", false, false,
                "*_여_*.pal", updateHeadName);
            CopyFolder(Path.Combine(dataDir, "palette/머리"), "Assets/Sprites/Characters/HeadMale/Palette/", false, false,
                "*_남_*.pal", updateHeadName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/머리통/남"), "Assets/Sprites/Characters/HeadMale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/머리통/여"), "Assets/Sprites/Characters/HeadFemale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/남"), "Assets/Sprites/Characters/BodyMale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/여"), "Assets/Sprites/Characters/BodyFemale/");

            //CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/남"), "Assets/Sprites/Characters/BodyMale/");
            //CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/여"), "Assets/Sprites/Characters/BodyFemale/");
            CopyFolder(Path.Combine(dataDir, "texture/유저인터페이스/illust"), "Assets/Sprites/Cutins/");

            CopyFolder(Path.Combine(dataDir, "sprite/인간족/성직자"), "Assets/Sprites/Weapons/Acolyte/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/궁수"), "Assets/Sprites/Weapons/Archer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/마법사"), "Assets/Sprites/Weapons/Mage/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/상인"), "Assets/Sprites/Weapons/Merchant/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/초보자"), "Assets/Sprites/Weapons/Novice/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/검사"), "Assets/Sprites/Weapons/Swordsman/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/도둑"), "Assets/Sprites/Weapons/Thief/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/슈퍼노비스"), "Assets/Sprites/Weapons/SuperNovice/", false, true,
                "*", UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/기사"), "Assets/Sprites/Weapons/Knight/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/위저드"), "Assets/Sprites/Weapons/Wizard/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/프리스트"), "Assets/Sprites/Weapons/Priest/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/헌터"), "Assets/Sprites/Weapons/Hunter/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/어세신"), "Assets/Sprites/Weapons/Assassin/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/제철공"), "Assets/Sprites/Weapons/Blacksmith/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/크루세이더"), "Assets/Sprites/Weapons/Crusader/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/세이지"), "Assets/Sprites/Weapons/Sage/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/바드"), "Assets/Sprites/Weapons/Bard/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/무희"), "Assets/Sprites/Weapons/Dancer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몽크"), "Assets/Sprites/Weapons/Monk/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/로그"), "Assets/Sprites/Weapons/Rogue/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/연금술사"), "Assets/Sprites/Weapons/Alchemist/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/운영자"), "Assets/Sprites/Weapons/GameMaster/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/신페코크루세이더"), "Assets/Sprites/Weapons/PecoCrusader/", false,
                true, "*", UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/페코페코_기사"), "Assets/Sprites/Weapons/PecoKnight/", false, true,
                "*", UpdateSpriteName);

            CopyFolder(Path.Combine(dataDir, "sprite/방패/성직자"), "Assets/Sprites/Shields/Acolyte/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/궁수"), "Assets/Sprites/Shields/Archer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/마법사"), "Assets/Sprites/Shields/Mage/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/상인"), "Assets/Sprites/Shields/Merchant/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/초보자"), "Assets/Sprites/Shields/Novice/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/검사"), "Assets/Sprites/Shields/Swordsman/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/도둑"), "Assets/Sprites/Shields/Thief/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/슈퍼노비스"), "Assets/Sprites/Shields/SuperNovice/", false, true,
                "*", UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/기사"), "Assets/Sprites/Shields/Knight/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/위저드"), "Assets/Sprites/Shields/Wizard/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/프리스트"), "Assets/Sprites/Shields/Priest/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/헌터"), "Assets/Sprites/Shields/Hunter/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/어세신"), "Assets/Sprites/Shields/Assassin/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/제철공"), "Assets/Sprites/Shields/Blacksmith/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/크루세이더"), "Assets/Sprites/Shields/Crusader/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/세이지"), "Assets/Sprites/Shields/Sage/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/바드"), "Assets/Sprites/Shields/Bard/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/무희"), "Assets/Sprites/Shields/Dancer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/몽크"), "Assets/Sprites/Shields/Monk/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/로그"), "Assets/Sprites/Shields/Rogue/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/연금술사"), "Assets/Sprites/Shields/Alchemist/", false, true, "*",
                UpdateSpriteName);
            //CopyFolder(Path.Combine(dataDir, "sprite/방패/운영자"), "Assets/Sprites/Shields/GameMaster/", false, true, "*",
            //    UpdateSpriteName);
            //CopyFolder(Path.Combine(dataDir, "sprite/방패/신페코크루세이더"), "Assets/Sprites/Shields/PecoCrusader/", false, true,
            //    "*", UpdateSpriteName);
            //CopyFolder(Path.Combine(dataDir, "sprite/방패/페코페코_기사"), "Assets/Sprites/Shields/PecoKnight/", false, true,
            //    "*", UpdateSpriteName);

            CopySingleFile(Path.Combine(dataDir, "sprite/cursors.act"), "Assets/Sprites/Misc/");
            CopySingleFile(Path.Combine(dataDir, "sprite/cursors.spr"), "Assets/Sprites/Misc/");
            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/emotion.act"), "Assets/Sprites/Misc/");
            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/emotion.spr"), "Assets/Sprites/Misc/");
            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/숫자.act"), "Assets/Sprites/Misc/damagenumbers.act");
            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/숫자.spr"), "Assets/Sprites/Misc/damagenumbers.spr");

            //the project has custom monsters, but for copyright reasons the sprites aren't part of the repo
            //to make things still run without the custom sprites we substitute a similar sprite if necessary
            CreateTemporarySpriteIfRequired("andre", "andre_larva");
            CreateTemporarySpriteIfRequired("deniro", "deniro_larva");
            CreateTemporarySpriteIfRequired("piere", "piere_larva");
            CreateTemporarySpriteIfRequired("andre", "soldier_andre");
            CreateTemporarySpriteIfRequired("deniro", "soldier_deniro");
            CreateTemporarySpriteIfRequired("piere", "soldier_piere");
            CreateTemporarySpriteIfRequired("vagabond_wolf", "were_wolf");
            CreateTemporarySpriteIfRequired("frilldora", "raptice");
            CreateTemporarySpriteIfRequired("poison_spore", "deathspore");

            RunPostCopyProcessing(openLightingManager: true);
        }

        private static void RunPostCopyProcessing(bool openLightingManager)
        {
            AssetDatabase.Refresh();

            EffectStrImporter.Import(); //effects
            EffectStrImporter.ImportEffectTextures();
            RagnarokMapImporterWindow.ImportWater();
            RagnarokMapImporterWindow.ImportAllMissingMaps();
            ItemIconImporter.ImportItems();

            if (openLightingManager)
                RoLightingManagerWindow.CreateOrOpen();
        }

        private class CopyFilePlan
        {
            public string SourcePath;
            public string DestinationPath;
        }

        private static List<CopyFilePlan> BuildCopyFilePlan(
            string sourceDirectory,
            string destinationDirectory,
            bool recursive = false,
            bool maleFemaleSplit = false,
            string filter = "*",
            Func<string, string> updateFileName = null)
        {
            var copyFilePlan = new List<CopyFilePlan>();

            if (!Directory.Exists(sourceDirectory))
                return copyFilePlan;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var sourcePath in Directory.GetFiles(sourceDirectory, filter, searchOption))
            {
                copyFilePlan.Add(new CopyFilePlan
                {
                    SourcePath = sourcePath,
                    DestinationPath = GetCopyDestinationPath(
                        sourceDirectory,
                        sourcePath,
                        destinationDirectory,
                        maleFemaleSplit,
                        updateFileName
                    )
                });
            }

            return copyFilePlan;
        }

        private static string GetCopyDestinationPath(
            string sourceDirectory,
            string sourcePath,
            string destinationDirectory,
            bool maleFemaleSplit,
            Func<string, string> updateFileName)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);

            if (maleFemaleSplit)
            {
                if (relativePath.Contains("_남_"))
                    destinationPath = Path.Combine(destinationDirectory, "Male", relativePath);

                if (relativePath.Contains("_여_"))
                    destinationPath = Path.Combine(destinationDirectory, "Female", relativePath);
            }

            if (updateFileName != null)
                destinationPath = updateFileName(destinationPath);

            if (Path.GetExtension(sourcePath).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                destinationPath = Path.ChangeExtension(destinationPath, ".png");

            return destinationPath;
        }

        [MenuItem("Ragnarok/Client Data Health Check", priority = 2)]
        public static void ShowCopyClientDataWindow()
        {
            var window = GetWindow<RagnarokCopyFromRealClientWindow>("Client Data Health Check");
            window.minSize = new Vector2(450, 600);
            window.Focus();
        }

        public class RagnarokCopyFromRealClientWindow : EditorWindow
        {
            private struct CopyCategory
            {
                public string Label;
                public Action Execute;
                public Func<bool> IsAlreadyImported;
                public Func<ImportHealth> GetHealth;
            }

            private List<CopyCategory> categories;
            private List<ImportTaskState> taskStates;
            private Vector2 scrollPos;
            private string dataDir;

            private CopyCategory CreateCopyCategory(
                string label,
                string sourceRelativePath,
                string destinationDirectory,
                bool recursive = false,
                bool maleFemaleSplit = false,
                string filter = "*",
                Func<string, string> updateFileName = null)
            {
                var sourceDirectory = Path.Combine(dataDir, sourceRelativePath);

                return new CopyCategory
                {
                    Label = label,
                    Execute = () => CopyFolder(
                        sourceDirectory,
                        destinationDirectory,
                        recursive,
                        maleFemaleSplit,
                        filter,
                        updateFileName
                    ),
                    IsAlreadyImported = () => IsCopyFolderComplete(
                        sourceDirectory,
                        destinationDirectory,
                        recursive,
                        maleFemaleSplit,
                        filter,
                        updateFileName
                    ),
                    GetHealth = () => GetCopyFolderHealth(
                        sourceDirectory,
                        destinationDirectory,
                        recursive,
                        maleFemaleSplit,
                        filter,
                        updateFileName
                    )
                };
            }



            private class ImportHealth
            {
                public int Present;
                public int Total;
                public string UnitLabel = "files";
                public string PresentLabel = "present";
                public string ErrorMessage;
                public int Missing => Math.Max(0, Total - Present);
                public bool HasProgress => Total > 0;
                public bool IsComplete => string.IsNullOrWhiteSpace(ErrorMessage) && Total > 0 && Missing == 0;

                public string ToDisplayString()
                {
                    if (!string.IsNullOrWhiteSpace(ErrorMessage))
                        return ErrorMessage;

                    if (Total == 0)
                        return $"No source {UnitLabel} found";

                    return $"{Present}/{Total} {UnitLabel} {PresentLabel}, {Missing} missing";
                }
            }

            private class ImportTaskState
            {
                public CopyCategory Category;
                public bool NeedsImport;
                public bool Selected;
                public string Status;
                public ImportHealth Health;
            }

            private static string UpdateJobSpriteName(string path)
            {
                foreach (var mapping in RagnarokClientDataImportDefinitions.JobSpriteMappings)
                    path = path.Replace(mapping.SourceName, mapping.DestinationName);

                return path.Replace("여_", "F_").Replace("남_", "M_");
            }

            private static bool ShouldImportJobSpriteMapping(
                RagnarokClientDataImportDefinitions.JobSpriteMapping mapping,
                string destinationRoot)
            {
                if (!string.Equals(destinationRoot, "Shields", StringComparison.Ordinal))
                    return true;

                return !RagnarokClientDataImportDefinitions.ShieldSpriteSourceNameExceptions.Contains(mapping.SourceName);
            }

            private static ImportHealth GetJobSpriteHealth(string dataDir, string sourceRoot, string destinationRoot)
            {
                var copyFilePlan = new List<CopyFilePlan>();

                foreach (var mapping in RagnarokClientDataImportDefinitions.JobSpriteMappings)
                {
                    if (!ShouldImportJobSpriteMapping(mapping, destinationRoot))
                        continue;

                    copyFilePlan.AddRange(BuildCopyFilePlan(
                        Path.Combine(dataDir, sourceRoot, mapping.SourceName),
                        $"Assets/Sprites/{destinationRoot}/{mapping.DestinationName}/",
                        maleFemaleSplit: true,
                        updateFileName: UpdateJobSpriteName
                    ));
                }

                return GetCopyFilePlanHealth(copyFilePlan);
            }

            private static bool IsJobSpriteImportComplete(string dataDir, string sourceRoot, string destinationRoot)
            {
                return GetJobSpriteHealth(dataDir, sourceRoot, destinationRoot).IsComplete;
            }

            private static void CopyJobSprites(string dataDir, string sourceRoot, string destinationRoot)
            {
                foreach (var mapping in RagnarokClientDataImportDefinitions.JobSpriteMappings)
                {
                    if (!ShouldImportJobSpriteMapping(mapping, destinationRoot))
                        continue;

                    CopyFolder(
                        Path.Combine(dataDir, sourceRoot, mapping.SourceName),
                        $"Assets/Sprites/{destinationRoot}/{mapping.DestinationName}/",
                        maleFemaleSplit: true,
                        updateFileName: UpdateJobSpriteName
                    );
                }
            }

            private static bool HasAnyFile(string path, string pattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                return Directory.Exists(path) && Directory.GetFiles(path, pattern, searchOption).Any();
            }





            private static ImportHealth GetCopyFolderHealth(
                string sourceDirectory,
                string destinationDirectory,
                bool recursive = false,
                bool maleFemaleSplit = false,
                string filter = "*",
                Func<string, string> updateFileName = null)
            {
                return GetCopyFilePlanHealth(BuildCopyFilePlan(
                    sourceDirectory,
                    destinationDirectory,
                    recursive,
                    maleFemaleSplit,
                    filter,
                    updateFileName
                ));
            }

            private static ImportHealth GetCopyFilePlanHealth(IEnumerable<CopyFilePlan> copyFilePlan)
            {
                var health = new ImportHealth();

                foreach (var copyFile in copyFilePlan)
                {
                    health.Total++;

                    if (File.Exists(copyFile.DestinationPath))
                        health.Present++;
                }

                return health;
            }

            private static ImportHealth GetFixedFileHealth(IEnumerable<string> destinationPaths)
            {
                var health = new ImportHealth();

                foreach (var destinationPath in destinationPaths)
                {
                    health.Total++;

                    if (File.Exists(destinationPath))
                        health.Present++;
                }

                return health;
            }

            private static bool IsCopyFolderComplete(
                string sourceDirectory,
                string destinationDirectory,
                bool recursive = false,
                bool maleFemaleSplit = false,
                string filter = "*",
                Func<string, string> updateFileName = null)
            {
                return GetCopyFolderHealth(
                    sourceDirectory,
                    destinationDirectory,
                    recursive,
                    maleFemaleSplit,
                    filter,
                    updateFileName
                ).IsComplete;
            }



            private static IEnumerable<string> GetMiscellaneousDestinationPaths()
            {
                return RagnarokClientDataImportDefinitions.MiscellaneousFiles
                    .Select(fileImport => fileImport.DestinationPath);
            }

            private ImportHealth GetMiscellaneousFileHealth()
            {
                return GetFixedFileHealth(GetMiscellaneousDestinationPaths());
            }

            private bool HasMiscellaneousFiles()
            {
                return GetMiscellaneousFileHealth().IsComplete;
            }

            private static bool HasGeneratedEffectPrefabs()
            {
                return HasAnyFile("Assets/Effects/Prefabs", "*.prefab");
            }

            private static bool HasSkillEffectAtlas()
            {
                return File.Exists("Assets/Textures/Resources/SkillAtlas.spriteatlasv2") &&
                       File.Exists("Assets/Textures/Import/FireBolt1.png") &&
                       File.Exists("Assets/Textures/Import/coin_a.png") &&
                       File.Exists("Assets/Textures/Resources/BigBang.png");
            }

            private ImportHealth GetWaterTextureHealth()
            {
                return GetCopyFolderHealth(
                    Path.Combine(dataDir, RagnarokClientDataImportDefinitions.WaterSourceRelativePath),
                    RagnarokClientDataImportDefinitions.WaterDestinationDirectory,
                    filter: "water*.jpg"
                );
            }

            private bool HasImportedWaterTextures()
            {
                return GetWaterTextureHealth().IsComplete;
            }

            [Serializable]
            private class MapConfigurationWrapper
            {
                public List<MapConfigurationEntry> Items;
            }

            [Serializable]
            private class MapConfigurationEntry
            {
                public string Code;
            }

            private static ImportHealth GetMapImportHealth()
            {
                var mapConfigurationPath = RagnarokClientDataImportDefinitions.MapConfigurationPath;
                var mapSceneDirectory = RagnarokClientDataImportDefinitions.MapSceneDirectory;

                var health = new ImportHealth { UnitLabel = "maps", PresentLabel = "imported" };

                if (!File.Exists(mapConfigurationPath))
                {
                    health.ErrorMessage = $"Missing map configuration: {mapConfigurationPath}";
                    return health;
                }

                try
                {
                    var mapConfiguration = JsonUtility.FromJson<MapConfigurationWrapper>(
                        File.ReadAllText(mapConfigurationPath)
                    );

                    var maps = mapConfiguration?.Items ?? new List<MapConfigurationEntry>();
                    health.Total = maps.Count;

                    foreach (var map in maps)
                    {
                        if (string.IsNullOrWhiteSpace(map.Code))
                            continue;

                        var mapScenePath = Path.Combine(mapSceneDirectory, map.Code + ".unity")
                            .Replace("\\", "/");

                        if (File.Exists(mapScenePath))
                            health.Present++;
                    }
                }
                catch (Exception exception)
                {
                    health.ErrorMessage = $"Could not read map configuration: {exception.Message}";
                }

                return health;
            }

            private static bool HasImportedMapAssets()
            {
                return GetMapImportHealth().IsComplete;
            }

            private static ImportHealth GetMapHealth()
            {
                return GetMapImportHealth();
            }


            private static bool HasImportedItemIcons()
            {
                return File.Exists("Assets/Textures/ItemAtlas.spriteatlasv2") &&
                       HasAnyFile("Assets/Sprites/Imported/Icons/Sprites", "*.png") &&
                       HasAnyFile("Assets/Sprites/Imported/Collections", "*.png");
            }

            private static ImportHealth GetTemporaryMonsterAliasHealth()
            {
                return GetFixedFileHealth(
                    RagnarokClientDataImportDefinitions.TemporaryMonsterAliases.SelectMany(alias => new[]
                    {
                        $"Assets/Sprites/Monsters/{alias.AliasSpriteName}.spr",
                        $"Assets/Sprites/Monsters/{alias.AliasSpriteName}.act"
                    })
                );
            }

            private static bool HasTemporaryMonsterAliases()
            {
                return GetTemporaryMonsterAliasHealth().IsComplete;
            }

            private static void CreateTemporaryMonsterAliases()
            {
                foreach (var alias in RagnarokClientDataImportDefinitions.TemporaryMonsterAliases)
                    CreateTemporarySpriteIfRequired(alias.SourceSpriteName, alias.AliasSpriteName);
            }

            private void RunHealthCheck()
            {
                if (categories == null)
                    return;

                var previousSelection = taskStates == null
                    ? new Dictionary<string, bool>()
                    : taskStates.ToDictionary(taskState => taskState.Category.Label, taskState => taskState.Selected);

                taskStates = new List<ImportTaskState>(categories.Count);

                foreach (var category in categories)
                {
                    var taskState = new ImportTaskState
                    {
                        Category = category
                    };

                    try
                    {
                        taskState.Health = category.GetHealth?.Invoke();

                        var isImported = taskState.Health != null
                            ? taskState.Health.IsComplete
                            : category.IsAlreadyImported();

                        taskState.NeedsImport = !isImported;
                        taskState.Status = isImported ? "OK" : "Needs import";

                        taskState.Selected = previousSelection.TryGetValue(category.Label, out var wasSelected)
                            ? taskState.NeedsImport && wasSelected
                            : taskState.NeedsImport;
                    }
                    catch (Exception exception)
                    {
                        taskState.NeedsImport = true;
                        taskState.Selected = previousSelection.TryGetValue(category.Label, out var wasSelected) ? wasSelected : true;
                        taskState.Status = "Error";
                        taskState.Health = new ImportHealth { ErrorMessage = exception.Message };

                        Debug.LogError(
                            $"[Health Check Failure] Category = '{category.Label}'\n" +
                            $"Exception message: {exception.Message}\n" +
                            $"Full stack trace:\n{exception}"
                        );
                    }

                    taskStates.Add(taskState);
                }

                Repaint();
            }


            private void SelectMissing()
            {
                if (taskStates == null)
                    return;

                foreach (var taskState in taskStates)
                    taskState.Selected = taskState.NeedsImport;

                Repaint();
            }


            private void ClearSelection()
            {
                if (taskStates == null)
                    return;

                foreach (var taskState in taskStates)
                    taskState.Selected = false;

                Repaint();
            }


            private void ImportMissing()
            {
                var previousSelection = taskStates == null
                    ? new Dictionary<string, bool>()
                    : taskStates.ToDictionary(taskState => taskState.Category.Label, taskState => taskState.Selected);

                RunHealthCheck();

                if (taskStates == null)
                    return;

                foreach (var taskState in taskStates)
                {
                    if (previousSelection.TryGetValue(taskState.Category.Label, out var wasSelected))
                        taskState.Selected = taskState.NeedsImport && wasSelected;
                }

                var importedTaskCount = 0;
                var refreshedBeforePostProcess = false;

                foreach (var taskState in taskStates)
                {
                    if (!taskState.NeedsImport || !taskState.Selected)
                        continue;

                    try
                    {
                        if (!refreshedBeforePostProcess &&
                            taskState.Category.Label.StartsWith("Post-process:", StringComparison.Ordinal))
                        {
                            AssetDatabase.Refresh();
                            refreshedBeforePostProcess = true;
                        }

                        taskState.Category.Execute();
                        importedTaskCount++;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"[Import Missing Failure] Category = '{taskState.Category.Label}'\n" +
                            $"Exception message: {exception.Message}\n" +
                            $"Full stack trace:\n{exception}"
                        );
                    }
                }

                AssetDatabase.Refresh();
                RunHealthCheck();
                Debug.Log($"Imported {importedTaskCount} selected missing client data tasks.");
            }



            private void OnEnable()
            {
                dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
                if (dataDir == null)
                {
                    const string prompt =
                        "Before you continue, specify the extracted data.grf directory with correct locale filenames.";

                    if (!EditorUtility.DisplayDialog("Copy from RO Client", prompt, "Browse", "Cancel"))
                        return;

                    RagnarokDirectory.SetDataDirectory();
                    dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
                    if (string.IsNullOrWhiteSpace(dataDir))
                        return;
                }

                // sanity checks
                bool TestPath(string fileName)
                {
                    var normalizedFileName = fileName.Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar);
                    var full = Path.Combine(dataDir, normalizedFileName);

                    if (!File.Exists(full))
                    {
                        Debug.LogError($"Invalid client data directory: missing {normalizedFileName}");
                        return false;
                    }

                    return true;
                }

                if (!TestPath("prontera.gat") || !TestPath("texture/워터/water000.jpg"))
                    return;

                // Define each copy category
                categories = new List<CopyCategory>
                {
                    CreateCopyCategory("Sounds (WAV)", "wav/", "Assets/Sounds/", recursive: true),
                    CreateCopyCategory("Monster Sprites", "sprite/몬스터", "Assets/Sprites/Monsters/"),
                    CreateCopyCategory("Headgear Sprites (Male)", "sprite/악세사리/남", "Assets/Sprites/Headgear/Male/"),
                    CreateCopyCategory("Headgear Sprites (Female)", "sprite/악세사리/여", "Assets/Sprites/Headgear/Female/"),
                    CreateCopyCategory("NPC Sprites", "sprite/npc", "Assets/Sprites/Npcs/"),
                    CreateCopyCategory("Effect Sprites", "sprite/이팩트", "Assets/Sprites/Effects/"),
                    CreateCopyCategory("Head Palettes (Female)", "palette/몸", "Assets/Sprites/Characters/HeadFemale/", filter: "*_여_*.pal"),
                    CreateCopyCategory("Head Palettes (Male)", "palette/몸", "Assets/Sprites/Characters/HeadMale/", filter: "*_남_*.pal"),
                    CreateCopyCategory("Character Heads (Male)", "sprite/인간족/머리통/남", "Assets/Sprites/Characters/HeadMale/"),
                    CreateCopyCategory("Character Heads (Female)", "sprite/인간족/머리통/여", "Assets/Sprites/Characters/HeadFemale/"),
                    CreateCopyCategory("Character Bodies (Male)", "sprite/인간족/몸통/남", "Assets/Sprites/Characters/BodyMale/"),
                    CreateCopyCategory("Character Bodies (Female)", "sprite/인간족/몸통/여", "Assets/Sprites/Characters/BodyFemale/"),
                    CreateCopyCategory("UI Illustrations", "texture/유저인터페이스/illust", "Assets/Sprites/Cutins/"),
                    new CopyCategory
                    {
                        Label = "Weapon Sprites (All Classes)",
                        Execute = () => CopyJobSprites(dataDir, "sprite/인간족", "Weapons"),
                        IsAlreadyImported = () => IsJobSpriteImportComplete(dataDir, "sprite/인간족", "Weapons"),
                        GetHealth = () => GetJobSpriteHealth(dataDir, "sprite/인간족", "Weapons")
                    },
                    new CopyCategory
                    {
                        Label = "Shield Sprites (All Classes)",
                        Execute = () => CopyJobSprites(dataDir, "sprite/방패", "Shields"),
                        IsAlreadyImported = () => IsJobSpriteImportComplete(dataDir, "sprite/방패", "Shields"),
                        GetHealth = () => GetJobSpriteHealth(dataDir, "sprite/방패", "Shields")
                    },
                    new CopyCategory
                    {
                        Label = "Miscellaneous Files (Cursors, Emotions, Damagenumbers)",
                        Execute = () =>
                        {
                            foreach (var fileImport in RagnarokClientDataImportDefinitions.MiscellaneousFiles)
                            {
                                CopySingleFile(
                                    Path.Combine(dataDir, fileImport.SourceRelativePath),
                                    fileImport.DestinationPath
                                );
                            }
                        },
                        IsAlreadyImported = HasMiscellaneousFiles,
                        GetHealth = GetMiscellaneousFileHealth
                    },
                    new CopyCategory
                    {
                        Label = "Post-process: Monster Sprite Aliases",
                        Execute = CreateTemporaryMonsterAliases,
                        IsAlreadyImported = HasTemporaryMonsterAliases,
                        GetHealth = GetTemporaryMonsterAliasHealth
                    },
                    new CopyCategory
                    {
                        Label = "Post-process: Effect Prefabs",
                        Execute = EffectStrImporter.Import,
                        IsAlreadyImported = HasGeneratedEffectPrefabs
                    },
                    new CopyCategory
                    {
                        Label = "Post-process: Skill Effect Atlas",
                        Execute = EffectStrImporter.ImportEffectTextures,
                        IsAlreadyImported = HasSkillEffectAtlas
                    },
                    new CopyCategory
                    {
                        Label = "Post-process: Water Textures",
                        Execute = RagnarokMapImporterWindow.ImportWater,
                        IsAlreadyImported = HasImportedWaterTextures,
                        GetHealth = GetWaterTextureHealth
                    },
                    new CopyCategory
                    {
                        Label = "Post-process: Missing Maps",
                        Execute = RagnarokMapImporterWindow.ImportAllMissingMaps,
                        IsAlreadyImported = HasImportedMapAssets,
                        GetHealth = GetMapHealth
                    },
                    new CopyCategory
                    {
                        Label = "Post-process: Skill and Item Icons",
                        Execute = ItemIconImporter.ImportItems,
                        IsAlreadyImported = HasImportedItemIcons
                    }
                };

                RunHealthCheck();
            }



            private static readonly Color HealthBarBackground = new Color(0.15f, 0.15f, 0.15f, 1f);
            private static readonly Color HealthBarPresent = new Color(0.20f, 0.62f, 0.32f, 1f);
            private static readonly Color HealthBarMissing = new Color(0.82f, 0.55f, 0.16f, 1f);

            private static GUIStyle _healthBarTextStyle;

            private static GUIStyle HealthBarTextStyle
            {
                get
                {
                    if (_healthBarTextStyle != null)
                        return _healthBarTextStyle;

                    _healthBarTextStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };

                    _healthBarTextStyle.normal.textColor = Color.white;
                    return _healthBarTextStyle;
                }
            }

            private static void DrawHealthProgressBar(ImportHealth health)
            {
                if (health == null || !health.HasProgress)
                    return;

                var presentRatio = Mathf.Clamp01((float)health.Present / health.Total);
                var missingRatio = Mathf.Clamp01((float)health.Missing / health.Total);

                var rect = GUILayoutUtility.GetRect(18f, 14f, GUILayout.ExpandWidth(true));

                EditorGUI.DrawRect(rect, HealthBarBackground);

                if (presentRatio > 0f)
                {
                    var presentRect = new Rect(rect.x, rect.y, rect.width * presentRatio, rect.height);
                    EditorGUI.DrawRect(presentRect, HealthBarPresent);
                }

                if (missingRatio > 0f)
                {
                    var missingRect = new Rect(rect.x + rect.width * presentRatio, rect.y, rect.width * missingRatio, rect.height);
                    EditorGUI.DrawRect(missingRect, HealthBarMissing);
                }

                var importedLabel = string.IsNullOrWhiteSpace(health.PresentLabel)
                    ? "present"
                    : health.PresentLabel;

                var label = $"{Mathf.RoundToInt(presentRatio * 100f)}% {importedLabel} / {Mathf.RoundToInt(missingRatio * 100f)}% missing";
                GUI.Label(rect, label, HealthBarTextStyle);
            }


            private void OnGUI()
            {
                const string postProcessPrefix = "Post-process: ";

                EditorGUILayout.LabelField("Client Data Import Health Check", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Data directory: {dataDir ?? "(not set)"}", EditorStyles.miniLabel);
                EditorGUILayout.Space();

                EditorGUILayout.HelpBox(
                    "Scan the imported client data and generated Unity assets. " +
                    "Select the rows you want, then import the selected missing tasks.",
                    MessageType.Info
                );

                if (categories == null)
                {
                    EditorGUILayout.HelpBox(
                        $"Could not initialize import tasks.\n\nCurrent data directory: {dataDir ?? "(not set)"}",
                        MessageType.Error
                    );

                    if (GUILayout.Button("Set Data Directory", GUILayout.Height(28)))
                    {
                        RagnarokDirectory.SetDataDirectory();
                        OnEnable();
                    }

                    return;
                }

                var completeTaskCount = taskStates == null ? 0 : taskStates.Count(taskState => !taskState.NeedsImport);
                var missingTaskCount = taskStates == null ? 0 : taskStates.Count(taskState => taskState.NeedsImport);
                var selectedTaskCount = taskStates == null ? 0 : taskStates.Count(taskState => taskState.Selected);
                var totalTaskCount = categories.Count;

                if (GUILayout.Button("Run Health Check", GUILayout.Height(32)))
                    RunHealthCheck();

                EditorGUILayout.Space();

                var summaryType = missingTaskCount == 0 ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox(
                    $"{completeTaskCount}/{totalTaskCount} tasks OK. {missingTaskCount} task(s) need import. {selectedTaskCount} selected.",
                    summaryType
                );

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                var shownRawHeader = false;
                var shownPostHeader = false;

                for (var i = 0; i < categories.Count; i++)
                {
                    var taskState = taskStates != null && i < taskStates.Count ? taskStates[i] : null;
                    var category = taskState?.Category ?? categories[i];
                    var isPostProcess = category.Label.StartsWith(postProcessPrefix, StringComparison.Ordinal);

                    if (!isPostProcess && !shownRawHeader)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Raw client data", EditorStyles.boldLabel);
                        shownRawHeader = true;
                    }

                    if (isPostProcess && !shownPostHeader)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Generated assets / post-processing", EditorStyles.boldLabel);
                        shownPostHeader = true;
                    }

                    var status = taskState?.Status ?? "Not checked";
                    var statusLabel = status == "OK"
                        ? "OK"
                        : status == "Error"
                            ? "ERROR"
                            : "NEEDS IMPORT";

                    var taskLabel = isPostProcess
                        ? category.Label.Substring(postProcessPrefix.Length)
                        : category.Label;

                    var canSelect = taskState != null && taskState.NeedsImport;
                    var isSelected = taskState != null && taskState.Selected;

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    EditorGUI.BeginDisabledGroup(!canSelect);
                    var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(18));
                    EditorGUI.EndDisabledGroup();

                    if (canSelect)
                        taskState.Selected = newSelected;

                    EditorGUILayout.LabelField(statusLabel, EditorStyles.miniBoldLabel, GUILayout.Width(105));

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(taskLabel);

                    var healthDetail = taskState?.Health?.ToDisplayString();
                    if (!string.IsNullOrWhiteSpace(healthDetail))
                    {
                        EditorGUILayout.LabelField(healthDetail, EditorStyles.miniLabel);
                        DrawHealthProgressBar(taskState.Health);
                    }

                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Selection actions", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Select Missing", GUILayout.Height(30)))
                    SelectMissing();

                if (GUILayout.Button("Clear Selection", GUILayout.Height(30)))
                    ClearSelection();

                GUI.enabled = selectedTaskCount > 0;
                if (GUILayout.Button($"Import Selected ({selectedTaskCount})", GUILayout.Height(30)))
                    ImportMissing();
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }



            // Reuse copy helpers from original

        }

        private static void CreateTemporarySpriteIfRequired(string dummySpriteName, string spriteName)
        {
            var destSprPath = $"Assets/Sprites/Monsters/{spriteName}.spr";
            var destActPath = $"Assets/Sprites/Monsters/{spriteName}.act";
            if (File.Exists(destSprPath) || File.Exists(destActPath))
                return;
            var dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
            CopySingleFile(Path.Combine(dataDir, $"sprite/몬스터/{dummySpriteName}.spr"), destSprPath);
            CopySingleFile(Path.Combine(dataDir, $"sprite/몬스터/{dummySpriteName}.act"), destActPath);
        }

        private static void CopySingleFile(string sourcePath, string destinationPath)
        {
            Debug.Log($"[CopySingleFile] Attempting to copy:\n    sourcePath = {sourcePath}\n    destinationPath = {destinationPath}");

            var resolvedDestinationPath = destinationPath;
            var destinationLooksLikeFolder = destinationPath.EndsWith("/") ||
                                             destinationPath.EndsWith("\\") ||
                                             Directory.Exists(destinationPath);

            if (destinationLooksLikeFolder)
            {
                var destinationFolder = destinationPath.TrimEnd('\\', '/');
                var sourceFileName = Path.GetFileName(sourcePath);
                resolvedDestinationPath = Path.Combine(destinationFolder, sourceFileName);
            }

            var parentDirectory = Path.GetDirectoryName(resolvedDestinationPath);
            if (string.IsNullOrEmpty(parentDirectory))
            {
                Debug.LogError($"[CopySingleFile] Invalid destination: {resolvedDestinationPath}");
                return;
            }

            if (!Directory.Exists(parentDirectory))
            {
                Debug.Log($"[CopySingleFile] Creating directory: {parentDirectory}");
                Directory.CreateDirectory(parentDirectory);
            }

            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"[CopySingleFile] Source not found: {sourcePath}");
                return;
            }

            try
            {
                File.Copy(sourcePath, resolvedDestinationPath, overwrite: true);
                Debug.Log($"[CopySingleFile] Successfully copied {sourcePath} → {resolvedDestinationPath}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CopySingleFile] Failed to copy {sourcePath} → {resolvedDestinationPath}\nException: {exception}");
            }
        }


        private static bool CopyFolder(
            string sourceDirectory,
            string destinationDirectory,
            bool recursive = false,
            bool maleFemaleSplit = false,
            string filter = "*",
            Func<string, string> updateFileName = null)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                Debug.LogError($"CopyFolder: source directory not found: {sourceDirectory}");
                return false;
            }

            var copyFilePlan = BuildCopyFilePlan(
                sourceDirectory,
                destinationDirectory,
                recursive,
                maleFemaleSplit,
                filter,
                updateFileName
            );

            if (copyFilePlan.Count == 0)
            {
                Debug.LogWarning($"CopyFolder: no files in '{sourceDirectory}' matching '{filter}'");
                return false;
            }

            foreach (var copyFile in copyFilePlan)
            {
                var destinationParentDirectory = Path.GetDirectoryName(copyFile.DestinationPath);
                if (!string.IsNullOrWhiteSpace(destinationParentDirectory) &&
                    !Directory.Exists(destinationParentDirectory))
                {
                    Directory.CreateDirectory(destinationParentDirectory);
                }

                if (Path.GetExtension(copyFile.SourcePath).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                {
                    var texture = TextureImportHelper.LoadTexture(copyFile.SourcePath);
                    TextureImportHelper.SaveAndUpdateTexture(texture, copyFile.DestinationPath, textureImporter =>
                    {
                        textureImporter.textureType = TextureImporterType.Sprite;
                        textureImporter.spriteImportMode = SpriteImportMode.Single;
                        textureImporter.crunchedCompression = false;
                        textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                    });
                }
                else if (!File.Exists(copyFile.DestinationPath))
                {
                    File.Copy(copyFile.SourcePath, copyFile.DestinationPath, true);
                }
            }

            return true;
        }


    }
}
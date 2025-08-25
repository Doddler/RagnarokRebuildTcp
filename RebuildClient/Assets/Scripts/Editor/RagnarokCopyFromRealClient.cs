using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Assets.Scripts;
using Assets.Scripts.Editor;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;

namespace Assets.Editor
{
    public class RagnarokCopyFromRealClient : EditorWindow
    {

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
        [UnityEditor.MenuItem("Ragnarok/Test New Process", priority = -100)]
        public static void TestNewProcess()
        {
            Debug.Log("Testing new process");
            var dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;

            if (dataDir == null)
            {
                const string prompt = @"Before you continue, you will need to specify a directory containing the contents of an extracted data.grf. "
                                      + "For this import process to work correctly, the files will need to have been extracted with the right locale and working korean file names.";

                if (!EditorUtility.DisplayDialog("Copy from RO Client", prompt, "Continue", "Cancel"))
                    return;

                RagnarokDirectory.SetDataDirectory();

                dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
                if (dataDir == null)
                    return;
            }
            Debug.Log("Instantiate new boss");
            Debug.Log("Start process");
            RoDataImportManager.ProcessGrfData(dataDir);
        }

        [UnityEditor.MenuItem("Ragnarok/Clear Imported Assets", priority = -99)]
        public static void ClearImportedAssets()
        {
            Debug.Log("Start Clearing Imported Assets");
            try
            {
                AssetDatabase.StartAssetEditing();
                var filesToDelete = AssetDatabase.FindAssets("*", new[] {"Assets/Sprites", "Assets/Sounds"});
                Debug.Log($"Found {filesToDelete.Length} assets to remove");
                foreach (var guid in filesToDelete)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        [UnityEditor.MenuItem("Ragnarok/Select GRF data to import", priority = -98)]
        public static void OpenSelectGrfDataToImportWindow()
        {
            var window = GetWindow<RagnarokSelectGrfDataToImportWindow>("Select GRF data to import");
            window.minSize = new Vector2(450, 600);
            window.Focus();
        }

        public class RagnarokSelectGrfDataToImportWindow : EditorWindow
        {
            private Vector2 scrollPosition;
            private bool[] selections;
            private GrfDataFilesCategorized grfData;
            
            private void OnEnable()
            {
                grfData = RoDataImportManager.ReadGrfDataFolder();
                selections = new bool[grfData.Categories.Length];
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("Select data to import:", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", GUILayout.Height(20)))
                    for (int i = 0; i < selections.Length; i++)
                        selections[i] = true;
                if (GUILayout.Button("Unselect All", GUILayout.Height(20)))
                    for (int i = 0; i < selections.Length; i++)
                        selections[i] = false;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                for (var i = 0; i < grfData.Categories.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    selections[i] = EditorGUILayout.ToggleLeft(grfData[i], selections[i]);
                    EditorGUILayout.LabelField($"imported {grfData.Categories[i].Imported} of {grfData.Categories[i].Count} files", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space();

                EditorGUILayout.HelpBox(
                    "Before you continue, you will need to specify a directory containing the contents of an extracted data.grf. " +
                    "For this import process to work correctly, the files will need to have been extracted with the right locale and working Korean file names.",
                    MessageType.Warning
                );
                
                if (GUILayout.Button("Copy Selected Data", GUILayout.Height(30)))
                {
                    if (selections.Any(x => !x))
                    {
                        var actualGrfDataToProcess =  new GrfDataFilesCategorized();
                        for (var i = 0; i < selections.Length; i++)
                        {
                            if (selections[i])
                                actualGrfDataToProcess.Categories[i].AddRange(grfData.Categories[i]);
                        }
                        RoDataImportManager.ProcessGrfData(actualGrfDataToProcess);
                    }
                    else
                    {
                        RoDataImportManager.ProcessGrfData(grfData);
                    }
                }
                
            }
        }
        
        [UnityEditor.MenuItem("Ragnarok/Copy data from client data folder", priority = 1)]
        public static void CopyClientData()
        {
            string dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;

            if (dataDir == null)
            {
                const string prompt = @"Before you continue, you will need to specify a directory containing the contents of an extracted data.grf. "
                                      + "For this import process to work correctly, the files will need to have been extracted with the right locale and working korean file names.";

                if (!EditorUtility.DisplayDialog("Copy from RO Client", prompt, "Continue", "Cancel"))
                    return;

                RagnarokDirectory.SetDataDirectory();

                dataDir = RagnarokDirectory.GetRagnarokDataDirectorySafe;
                if (dataDir == null)
                    return;
            }

            if (!TestPath("prontera.gat") || !TestPath(@"texture\워터\water000.jpg"))
                return;

            const string prompt2 = @"This import process will copy files from your data folder into this project. "
                                   + "Because this includes converting all maps and objects, expect this process to take more than an hour."
                                   + "\n\nWhen complete, the lighting window will load where you can bake the lighting for all the scenes (accessible via 'Ragnarok->Lighting Manager'). "
                                   + "You will also need to manually copy over your BGM into the music folder if you want music."
                                   + "\n\nLastly, before you run you will need to use 'Ragnarok->Update Addressables' to make sure everything can load.";

            if (!EditorUtility.DisplayDialog("Copy from RO Client", prompt2, "Continue", "Cancel"))
                return;

            CopyFolder(Path.Combine(dataDir, "wav/"), "Assets/Sounds/", true);
            CopyFolder(Path.Combine(dataDir, "sprite/몬스터"), "Assets/Sprites/Monsters/");
            CopyFolder(Path.Combine(dataDir, "sprite/악세사리/남"), "Assets/Sprites/Headgear/Male/");
            CopyFolder(Path.Combine(dataDir, "sprite/악세사리/여"), "Assets/Sprites/Headgear/Female/");
            CopyFolder(Path.Combine(dataDir, "sprite/npc"), "Assets/Sprites/Npcs/");
            CopyFolder(Path.Combine(dataDir, "sprite/이팩트"), "Assets/Sprites/Effects/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/머리통/남"), "Assets/Sprites/Characters/HeadMale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/머리통/여"), "Assets/Sprites/Characters/HeadFemale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/남"), "Assets/Sprites/Characters/BodyMale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/여"), "Assets/Sprites/Characters/BodyFemale/");
            CopyFolder(Path.Combine(dataDir, "palette/몸"), "Assets/Sprites/Characters/HeadFemale/", false, false,
                "*_여_*.pal");
            CopyFolder(Path.Combine(dataDir, "palette/몸"), "Assets/Sprites/Characters/HeadMale/", false, false,
                "*_남_*.pal");

            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/남"), "Assets/Sprites/Characters/BodyMale/");
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/몸통/여"), "Assets/Sprites/Characters/BodyFemale/");
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
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/무희바지"), "Assets/Sprites/Weapons/Dancer/", false, true, "*",
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
            CopyFolder(Path.Combine(dataDir, "sprite/인간족/페코페코_기사_남"), "Assets/Sprites/Weapons/PecoKnight/", false, true,
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
            CopyFolder(Path.Combine(dataDir, "sprite/방패/무희바지"), "Assets/Sprites/Shields/Dancer/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/몽크"), "Assets/Sprites/Shields/Monk/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/로그"), "Assets/Sprites/Shields/Rogue/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/연금술사"), "Assets/Sprites/Shields/Alchemist/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/운영자"), "Assets/Sprites/Shields/GameMaster/", false, true, "*",
                UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/신페코크루세이더"), "Assets/Sprites/Shields/PecoCrusader/", false, true,
                "*", UpdateSpriteName);
            CopyFolder(Path.Combine(dataDir, "sprite/방패/페코페코_기사_남"), "Assets/Sprites/Shields/PecoKnight/", false, true,
                "*", UpdateSpriteName);

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

            AssetDatabase.Refresh();

            EffectStrImporter.Import(); //effects
            EffectStrImporter.ImportEffectTextures();
            RagnarokMapImporterWindow.ImportWater();
            RagnarokMapImporterWindow.ImportAllMissingMaps();
            ItemIconImporter.ImportItems();

            RoLightingManagerWindow.CreateOrOpen();
            return;

            bool TestPath(string fileName)
            {
                if (File.Exists(Path.Combine(dataDir, fileName))) return true;
                Debug.LogError(
                    $"Could not verify client data directory \"{dataDir}\" is valid. File checked: {fileName} ");
                return false;
            }
        }

        [UnityEditor.MenuItem("Ragnarok/Select data to copy from client data folder", priority = 2)]
        public static void ShowCopyClientDataWindow()
        {
            var window = GetWindow<RagnarokPrepareClientDataFromGrfDataWindow>("Prepare Client Data");
            window.minSize = new Vector2(450, 600);
            window.Focus();
        }

        public class RagnarokPrepareClientDataFromGrfDataWindow : EditorWindow
        {
            private struct CopyCategory
            {
                public string Label;
                public Action Execute;
                public Func<bool> IsAlreadyImported;
            }

            private List<CopyCategory> categories;
            private bool[] selections;
            private Vector2 scrollPos;
            private string dataDir;

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
                    if (dataDir == null)
                        return;
                }

                // sanity checks
                if (!TestPath("prontera.gat") || !TestPath(@"texture\워터\water000.jpg"))
                    return;

                // Define each copy category
                categories = new List<CopyCategory>
                {
                    new CopyCategory
                    {
                        Label = "Sounds (WAV)",
                        Execute = () => CopyFolder(Path.Combine(dataDir, "wav/"), "Assets/Sounds/", recursive: true),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sounds") && 
                            Directory.GetFiles("Assets/Sounds", 
                                "*.wav", SearchOption.AllDirectories).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Monster Sprites",
                        Execute = () => ImportFromGrfFolder(Path.Combine(dataDir, "sprite/몬스터"), "Assets/Sprites/Monsters/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Monsters") &&
                            Directory.GetFiles("Assets/Sprites/Monsters", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Headgear Sprites (Male)",
                        Execute = () =>
                            ImportFromGrfFolder(Path.Combine(dataDir, "sprite/악세사리/남"), "Assets/Sprites/Headgear/Male/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Headgear/Male") && 
                            Directory.GetFiles("Assets/Sprites/Headgear/Male", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Headgear Sprites (Female)",
                        Execute = () =>
                            ImportFromGrfFolder(Path.Combine(dataDir, "sprite/악세사리/여"), "Assets/Sprites/Headgear/Female/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Headgear/Female") && 
                            Directory.GetFiles("Assets/Sprites/Headgear/Female", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "NPC Sprites",
                        Execute = () => ImportFromGrfFolder(Path.Combine(dataDir, "sprite/npc"), "Assets/Sprites/Npcs/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Npcs") && 
                            Directory.GetFiles("Assets/Sprites/Npcs",
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Effect Sprites",
                        Execute = () => ImportFromGrfFolder(Path.Combine(dataDir, "sprite/이팩트"), "Assets/Sprites/Effects/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Effects") && 
                            Directory.GetFiles("Assets/Sprites/Effects", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Character Heads (Male) - Import",
                        Execute = () =>  ImportFromGrfFolder(Path.Combine(dataDir, "sprite/인간족/머리통/남"),
                                "Assets/Sprites/Characters/HeadMale/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Characters/HeadMale") && 
                            Directory.GetFiles("Assets/Sprites/Characters/HeadMale", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Character Heads (Female) -  Import",
                        Execute = () => ImportFromGrfFolder(Path.Combine(dataDir, "sprite/인간족/머리통/여"),
                            "Assets/Sprites/Characters/HeadFemale/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Characters/HeadFemale") && 
                            Directory.GetFiles("Assets/Sprites/Characters/HeadFemale", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Character Bodies (Male)",
                        Execute = () => ImportFromGrfFolder(Path.Combine(dataDir, "sprite/인간족/몸통/남"),
                            "Assets/Sprites/Characters/BodyMale/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Characters/BodyMale") && 
                            Directory.GetFiles("Assets/Sprites/Characters/BodyMale", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Character Bodies (Female)",
                        Execute = () => ImportFromGrfFolder(Path.Combine(dataDir, "sprite/인간족/몸통/여"),
                            "Assets/Sprites/Characters/BodyFemale/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Characters/BodyFemale") && 
                            Directory.GetFiles("Assets/Sprites/Characters/BodyFemale", 
                                "*.spr", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Head Palettes (Female)",
                        Execute = () => CopyFolder(Path.Combine(dataDir, "palette/몸"),
                            "Assets/Sprites/Characters/HeadFemale/", recursive: false, maleFemaleSplit: false,
                            filter: "*_여_*.pal"),
                        IsAlreadyImported = () => 
                            Directory.Exists(@"Assets/Sprites/Characters/HeadFemale") && 
                            Directory.GetFiles("Assets/Sprites/Characters/HeadFemale", 
                                "*_여_*.pal",SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Head Palettes (Male)",
                        Execute = () => CopyFolder(Path.Combine(dataDir, "palette/몸"),
                            "Assets/Sprites/Characters/HeadMale/", recursive: false, maleFemaleSplit: false,
                            filter: "*_남_*.pal"),
                        IsAlreadyImported = () => 
                            Directory.Exists(@"Assets/Sprites/Characters/HeadMale") && 
                            Directory.GetFiles("Assets/Sprites/Characters/HeadMale", 
                                "*_남_*.pal", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "UI Illustrations",
                        Execute = () =>
                            CopyFolder(Path.Combine(dataDir, "texture/유저인터페이스/illust"), "Assets/Sprites/Cutins/"),
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Cutins") && 
                            Directory.GetFiles("Assets/Sprites/Cutins",
                                "*.bmp", SearchOption.TopDirectoryOnly).Any()
                    },
                    new CopyCategory
                    {
                        Label = "Weapon Sprites (All Classes)",
                        Execute = () =>
                        {
                            // TODO: Extract these into an importable asset so that these strings are centralized
                            var jobs = new (string kr, string en)[]
                            {
                                ("성직자", "Acolyte"), ("궁수", "Archer"), ("마법사", "Mage"), ("상인", "Merchant"), ("초보자", "Novice"), ("검사", "Swordsman"),
                                ("도둑", "Thief"), ("슈퍼노비스", "SuperNovice"), ("기사", "Knight"), ("위저드", "Wizard"), ("프리스트", "Priest"), ("헌터", "Hunter"),
                                ("어세신", "Assassin"), ("제철공", "Blacksmith"), ("크루세이더", "Crusader"), ("세이지", "Sage"), ("바드", "Bard"), ("무희바지", "Dancer"),
                                ("몽크", "Monk"), ("로그", "Rogue"), ("연금술사", "Alchemist"), ("운영자", "GameMaster"), ("신페코크루세이더", "PecoCrusader"), ("페코페코_기사_남", "PecoKnight")
                            };
                            foreach ((string kr, string en) job in jobs)
                                ImportFromGrfFolder(Path.Combine(dataDir, $"sprite/인간족/{job.kr}/"),
                                    $"Assets/Sprites/Weapons/{job.en}/", false, true, "*",
                                    (p) => p.Replace($"{job.kr}_", $"{job.en}_").Replace("여_", "F_")
                                        .Replace("남_", "M_"));
                        },
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Weapons") &&
                            Directory.GetDirectories("Assets/Sprites/Weapons").Any()
                    },
                    new CopyCategory
                    {
                        Label = "Shield Sprites (All Classes)",
                        Execute = () =>
                        {
                            var jobs = new (string kr, string en)[]
                            {
                                ("성직자", "Acolyte"), ("궁수", "Archer"), ("마법사", "Mage"), ("상인", "Merchant"), ("초보자", "Novice"), ("검사", "Swordsman"),
                                ("도둑", "Thief"), ("슈퍼노비스", "SuperNovice"), ("기사", "Knight"), ("위저드", "Wizard"), ("프리스트", "Priest"), ("헌터", "Hunter"),
                                ("어세신", "Assassin"), ("제철공", "Blacksmith"), ("크루세이더", "Crusader"), ("세이지", "Sage"), ("바드", "Bard"), ("무희바지", "Dancer"),
                                ("몽크", "Monk"), ("로그", "Rogue"), ("연금술사", "Alchemist"), ("운영자", "GameMaster"), ("신페코크루세이더", "PecoCrusader"), ("페코페코_기사_남", "PecoKnight")
                            };
                            foreach ((string kr, string en) job in jobs)
                                ImportFromGrfFolder(Path.Combine(dataDir, $"sprite/방패/{job.kr}/"),
                                    $"Assets/Sprites/Shields/{job.kr}/", false, true);
                        },
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Shields") &&
                            Directory.GetDirectories("Assets/Sprites/Shields").Any()
                    },
                    new CopyCategory
                    {
                        Label = "Miscellaneous Files (Cursors, Emotions, Damagenumbers)",
                        Execute = () =>
                        {
                            CopySingleFile(Path.Combine(dataDir, "sprite/cursors.act"), "Assets/Sprites/Misc/");
                            CopySingleFile(Path.Combine(dataDir, "sprite/cursors.spr"), "Assets/Sprites/Misc/");
                            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/emotion.act"), "Assets/Sprites/Misc/");
                            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/emotion.spr"), "Assets/Sprites/Misc/");
                            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/숫자.act"),
                                "Assets/Sprites/Misc/damagenumbers.act");
                            CopySingleFile(Path.Combine(dataDir, "sprite/이팩트/숫자.spr"),
                                "Assets/Sprites/Misc/damagenumbers.spr");
                        },
                        IsAlreadyImported = () =>
                            Directory.Exists("Assets/Sprites/Misc") &&
                            Directory.GetFiles("Assets/Sprites/Misc", "*.spr").Any()
                    }
                };

                // Initialize selections based on whether already imported
                selections = categories.Select(cat => !cat.IsAlreadyImported()).ToArray();
                return;

                
                bool TestPath(string fileName)
                {
                    string full = Path.Combine(dataDir, fileName);
                    if (File.Exists(full)) return true;
                    Debug.LogError($"Invalid client data directory: missing {fileName}");
                    return false;
                }
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("Select data to import:", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", GUILayout.Height(20)))
                    for (int i = 0; i < selections.Length; i++)
                        selections[i] = true;
                if (GUILayout.Button("Unselect All", GUILayout.Height(20)))
                    for (int i = 0; i < selections.Length; i++)
                        selections[i] = false;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                for (int i = 0; i < categories.Count; i++)
                {
                    selections[i] = EditorGUILayout.ToggleLeft(categories[i].Label, selections[i]);
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space();

                EditorGUILayout.HelpBox(
                    "Before you continue, you will need to specify a directory containing the contents of an extracted data.grf. " +
                    "For this import process to work correctly, the files will need to have been extracted with the right locale and working Korean file names.",
                    MessageType.Warning
                );

                if (GUILayout.Button("Copy Selected Data", GUILayout.Height(30)))
                {
                    for (int i = 0; i < categories.Count; i++)
                    {
                        if (!selections[i]) continue;
                        try
                        {
                            categories[i].Execute();
                        }
                        catch (Exception ex)
                        {
                            throw new FileLoadException(
                                $"[Copy Failure] Category = '{categories[i].Label}" +
                                $"Exception message: {ex.Message}\n" +
                                $"Full stack trace:\n{ex}");
                        }
                    }

                    AssetDatabase.Refresh();
                    //Debug.Log($"Copied {count} categories.");
                }
            }

            // Reuse copy helpers from original
            private static void ImportFromGrfFolder(string srcFolderPath, string destFolderPath, bool recursive = false,
                bool maleFemaleSplit = false, string searchPattern = "*", Func<string, string> updateFileName = null)
            {
                if (!Directory.Exists(srcFolderPath))
                    throw new DirectoryNotFoundException($"ImportFromGrfFolder: source directory not found: {srcFolderPath}");
                
                SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                string[] srcFilePathArray = Directory.GetFiles(srcFolderPath, searchPattern, searchOption);
                if (srcFilePathArray.Length == 0)
                    throw new FileNotFoundException($"CopyFolder: no files in '{srcFolderPath}' matching '{searchPattern}'");
                
                foreach (string srcFilePath in srcFilePathArray)
                {
                    string relativePath = Path.GetRelativePath(srcFolderPath, srcFilePath);
                    string assetFilePath = Path.Combine(destFolderPath, relativePath);
                    if (maleFemaleSplit)
                    {
                        if (relativePath.Contains("_남_"))
                            assetFilePath = Path.Combine(destFolderPath, "Male", relativePath);
                        if (relativePath.Contains("_여_"))
                            assetFilePath = Path.Combine(destFolderPath, "Female", relativePath);
                    }
                    
                    if (updateFileName != null) assetFilePath = updateFileName(assetFilePath);
                    string assetDirectoryPath = Path.GetDirectoryName(assetFilePath) ?? throw new NoNullAllowedException("Invalid directory on {destPath}");
                    Directory.CreateDirectory(assetDirectoryPath);
                    string fileExtension = Path.GetExtension(srcFilePath);
                    if (!fileExtension.Equals(".spr", StringComparison.OrdinalIgnoreCase)) continue;
                    //Debug.Log($"Importing file {srcFilePath} to {assetDirectoryPath}");
                    RoDataImportManager.ImportActFile(srcFilePath.Replace(".spr",".act"), assetDirectoryPath);
                }
            }
            private static void CopyFolder(string src, string dest, bool recursive = false,
                bool maleFemaleSplit = false, string filter = "*", Func<string, string> updateFileName = null)
            {
                if (!Directory.Exists(src))
                {
                    throw new DirectoryNotFoundException($"CopyFolder: source directory not found: {src}");
                }

                var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(src, filter, opt);
                if (files.Length == 0)
                {
                    throw new FileNotFoundException($"CopyFolder: no files in '{src}' matching '{filter}'");
                }

                foreach (var path in Directory.GetFiles(src, filter, opt))
                {
                    var rel = Path.GetRelativePath(src, path);
                    var destPath = Path.Combine(dest, rel);
                    if (maleFemaleSplit)
                    {
                        if (rel.Contains("_남_"))
                            destPath = Path.Combine(dest, "Male", rel);
                        if (rel.Contains("_여_")) destPath = Path.Combine(dest, "Female", rel);
                    }

                    if (updateFileName != null) destPath = updateFileName(destPath);
                    var dir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var ext = Path.GetExtension(path);
                    if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                    {
                        var tex = TextureImportHelper.LoadTexture(path);
                        TextureImportHelper.SaveAndUpdateTexture(tex, destPath.Replace(".bmp", ".png"), ti =>
                        {
                            ti.textureType = TextureImporterType.Sprite;
                            ti.spriteImportMode = SpriteImportMode.Single;
                            ti.crunchedCompression = false;
                            ti.textureCompression = TextureImporterCompression.CompressedHQ;
                        });
                    }
                    else
                    {
                        if (!File.Exists(destPath))
                            File.Copy(path, destPath, true);
                    }
                }
            }

            private static void CopySingleFile(string src, string dest)
            {
                //Debug.Log($"[CopySingleFile] Attempting to copy:\n    src = {src}\n    dest = {dest}");
                
                string destPath;
                bool looksLikeFolder = dest.EndsWith("/") || dest.EndsWith("\\") || Directory.Exists(dest);
                switch (looksLikeFolder)
                {
                    case true when (dest.StartsWith("Assets/") || dest.StartsWith("Assets\\")):
                    {
                        string folder = dest.TrimEnd('\\', '/');
                    
                        string assetsSubpath = folder.Substring("Assets/".Length);
                        destPath = Path.Combine(Application.dataPath, assetsSubpath);
                    
                        string fileName = Path.GetFileName(src);
                        destPath = Path.Combine(destPath, fileName);
                        break;
                    }
                    case true:
                    {
                        string folder = dest.TrimEnd('\\', '/');
                        string fileName = Path.GetFileName(src);
                        destPath = Path.Combine(folder, fileName);
                        break;
                    }
                    default:
                        destPath = dest;
                        break;
                }

                var parentDir = Path.GetDirectoryName(destPath);
                if (string.IsNullOrEmpty(parentDir))
                    throw new DirectoryNotFoundException($"CopySingleFile: parent directory not found: {destPath}");
                
                Directory.CreateDirectory(parentDir);
                
                if (!File.Exists(src))
                    throw new FileNotFoundException($"CopySingleFile: source file not found: {src}");
                
                try
                {
                    File.Copy(src, destPath, overwrite: true);
                    //Debug.Log($"[CopySingleFile] Successfully copied {src} → {destPath}");
                }
                catch (Exception ex)
                {
                    throw new FileLoadException($"[CopySingleFile] Failed to copy {src} → {destPath}\nException: {ex}");
                }
            }
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

        private static void CopySingleFile(string src, string dest)
        {
            Debug.Log($"[CopySingleFile] Attempting to copy:\n    src = {src}\n    dest = {dest}");

            string destPath = dest;
            bool looksLikeFolder = dest.EndsWith("/") || dest.EndsWith("\\") || Directory.Exists(dest);
            if (looksLikeFolder)
            {
                string folder = dest.TrimEnd('\\', '/');
                var fileName = Path.GetFileName(src);
                destPath = Path.Combine(folder, fileName);
            }

            var parentDir = Path.GetDirectoryName(destPath);
            if (string.IsNullOrEmpty(parentDir))
            {
                Debug.LogError($"[CopySingleFile] Invalid destination: {destPath}");
                return;
            }

            if (!Directory.Exists(parentDir))
            {
                Debug.Log($"[CopySingleFile] Creating directory: {parentDir}");
                Directory.CreateDirectory(parentDir);
            }

            if (!File.Exists(src))
            {
                Debug.LogError($"[CopySingleFile] Source not found: {src}");
                return;
            }

            try
            {
                File.Copy(src, destPath, overwrite: true);
                Debug.Log($"[CopySingleFile] Successfully copied {src} → {destPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopySingleFile] Failed to copy {src} → {destPath}\nException: {ex}");
            }
        }

        private static void CopyFolder(string sourcePath, string dest, bool recursive = false,
            bool maleFemaleSplit = false,
            string filter = "*",
            Func<string, string> updateFileName = null)
        {
            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            Directory.CreateDirectory(dest); //CreateDirectory already checks for directory existence

            foreach (var path in Directory.GetFiles(sourcePath, filter, opt))
            {
                var rel = Path.GetRelativePath(sourcePath, path);
                var destPath = Path.Combine(dest, rel);

                if (maleFemaleSplit)
                {
                    if (rel.Contains("_남_"))
                        destPath = Path.Combine(dest, "Male/", rel);
                    if (rel.Contains("_여_"))
                        destPath = Path.Combine(dest, "Female/", rel);
                }

                if (File.Exists(destPath.Replace(".bmp", ".png")))
                    continue;

                if (updateFileName != null)
                    destPath = updateFileName(destPath);

                var outDir = Path.GetDirectoryName(destPath);
                Directory.CreateDirectory(outDir ?? throw new InvalidOperationException("output directory must exist"));

                var ext = Path.GetExtension(path);

                if (ext == ".bmp")
                {
                    var tex = TextureImportHelper.LoadTexture(path);
                    TextureImportHelper.SaveAndUpdateTexture(tex, destPath.Replace(".bmp", ".png"), ti =>
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.crunchedCompression = false;
                        ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    });
                }
                else
                {
                    if (!File.Exists(destPath))
                        File.Copy(path, destPath, false);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
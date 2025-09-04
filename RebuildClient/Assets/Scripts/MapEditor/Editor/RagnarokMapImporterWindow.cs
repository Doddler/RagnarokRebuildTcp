using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.MapEditor.Editor.ObjectEditors;
using Assets.Scripts.Sprites;
using B83.Image.BMP;
using RebuildSharedData.ClientTypes;
using SFB;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Utility.Editor;
using Debug = UnityEngine.Debug;

// ReSharper disable StringIndexOfIsCultureSpecific.1

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokMapImporterWindow : EditorWindow
    {
        //[MenuItem("Ragnarok/Test Import Effect")]
        //static void TestImportEffect()
        //{

        //}

        public static void ImportAllMissingMaps()
        {
            var wrapper = JsonUtility.FromJson<Wrapper<ClientMapEntry>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/maps.json"));

            var maps = wrapper.Items;

            if (!Directory.Exists("Assets/Scenes/Maps/"))
                Directory.CreateDirectory("Assets/Scenes/Maps/");

            int imported = 0;
            List<string> failedMaps = new List<string>();
            foreach (var map in maps)
            {
                var scenePath = $"Assets/Scenes/Maps/{map.Code}.unity";
                if (File.Exists(scenePath))
                    continue;

                var gndPath = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, map.Code + ".gnd");
                if (File.Exists(gndPath))
                {
                    try
                    {
                        ImportMap(gndPath);
                        imported++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Exception generated while importing map {gndPath}!");
                        Debug.LogException(e);
                        failedMaps.Add("Import Error - " + gndPath);
                    }
                }
                else
                {
                    Debug.LogWarning($"Map file not found: {gndPath}");
                    failedMaps.Add("Map File not found - " + gndPath);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Map Import] Imported {imported} new map(s). Import failed for {failedMaps.Count} Map(s)");

            if (failedMaps.Count > 0)
            {
                string failedMapsList = "";
                foreach (string map in failedMaps)
                {
                    failedMapsList += map + "\n";
                }
                Debug.LogWarning($"[Map Import] Map import failed on: \n{failedMapsList}");
            }
        }


        [MenuItem("Ragnarok/Build Sprite Attack Timing")]
        public static void BuildSpriteAttackTiming()
        {
            var guids = AssetDatabase.FindAssets("t:RoSpriteData", new[]
            {
                "Assets/Sprites/Monsters",
                //"Assets/Sprites/Characters",
                //"Assets/Sprites/Npcs"
            });

            var output = new List<string>();
            var totalOut = new List<string>();

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<RoSpriteData>(path);
                var name = asset.Name;
                if (asset.Type != SpriteType.Monster && asset.Type != SpriteType.Monster2 &&
                    asset.Type != SpriteType.Pet)
                {
                    Debug.LogWarning($"Sprite {name} is not the right type (is {asset.Type})");
                    continue;
                }

                if (asset.Name.ToUpper() == "SASQUATCH")
                {
                    Debug.Log("AAAA" + RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack1));
                }

                var actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack1);
                if (actionId == -1)
                    actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack2);
                if (actionId == -1)
                    continue;

                //note: the first direction of an action is authoritative in terms of frame delay for all other directions
                var frames = asset.Actions[actionId].Frames;
                var found = false;

                totalOut.Add($"{name}:{frames.Length * asset.Actions[actionId].Delay}");

                for (var j = 0; j < frames.Length; j++)
                {
                    if (frames[j].IsAttackFrame)
                    {
                        var time = j * asset.Actions[actionId].Delay;
                        output.Add($"{name}:{time}");
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var pos = frames.Length - 2;
                    if (pos < 0)
                        pos = 0;
                    var time = pos * asset.Actions[actionId].Delay;
                    output.Add($"{name}:{time}");
                }
            }

            File.WriteAllLines(@"Assets/Sprites/AttackTiming.txt", output);
            File.WriteAllLines(@"Assets/Sprites/AttackLength.txt", totalOut);
        }

        private static string GetGroupName(string path)
        {
            //because it's easier in the long run if bundle names are alphanumeric, we'll translate korean paths to english names


            var folder = new DirectoryInfo(Path.GetDirectoryName(path)).Name;

            var idx = path.IndexOfOccurence("/", 4);
            if (idx > 0)
            {
                var basePath = path.Substring(0, idx + 1);
                folder = new DirectoryInfo(Path.GetDirectoryName(basePath)).Name;
            }

            return folder switch
            {
                "거북이섬" => "TurtleIsland",
                "게페니아" => "Geffenia",
                "글래스트" => "GlastHeim",
                "글래지하수로" => "GlastHeimSewers",
                "길드전" => "GuildWar",
                "나무잡초꽃" => "Foliage",
                "내부소품" => "PropsIndoor",
                "니플헤임" => "Niflheim",
                "대만" => "Taiwan",
                "던전" => "Dungeon",
                "동굴마을" => "CaveVillage",
                "등대섬" => "Lighthouse",
                "라헬" => "Rachel",
                "러시아" => "Russia",
                "리히타르젠" => "Lighthalzen",
                "모로코" => "Morroc",
                "무명섬" => "Nameless",
                "사막도시" => "DesertCity",
                "아요타야" => "Ayothaya",
                "아인브로크" => "Einbroch",
                "알데바란" => "Aldebaran",
                "알베르타" => "Alberta",
                "어비스" => "Abyss",
                "외부소품" => "PropsOutdoor",
                "용암동굴" => "Magma",
                "움발라" => "Umbala",
                "유노" => "Juno",
                "유노추가" => "Juno2",
                "유페로스" => "Juperos",
                "인던01" => "Instance01",
                "인던02" => "Instance02",
                "일본" => "Japan",
                "자와이" => "Jawaii",
                "전장" => "Battlegrounds",
                "중국" => "China",
                "지하감옥" => "Prison",
                "지하묘지" => "Catacombs",
                "집시마을" => "GypsyVillage",
                "크리스마스마을" => "ChristmasVillage",
                "타나토스" => "Thanatos",
                "토르화산" => "ThorVolcano",
                "페이욘" => "Payon",
                "프론테라" => "Prontera",
                "해변마을" => "BeachVillage",
                "휘겔" => "Hugel",
                "흑마법사방" => "BlackMagic",
                "히나마쯔리" => "Hinamatsuri",
                _ => folder
            };
        }

        [MenuItem("Ragnarok/Update Addressables (Fast)")]
        public static void UpdateAddressablesSprites()
        {
            UpdateAddressables(false);
        }

        //Having processModels set de-duplicates all model assets in scenes into their own addressables group.
        //Things will still work if you don't update them, but you can skip them if you don't want to wait.
        [MenuItem("Ragnarok/Update Addressables (Full)")]
        public static void UpdateAddressablesAll()
        {
            UpdateAddressables(true);
        }

        public static void UpdateAddressables(bool processModels = true)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var defGroup = settings.DefaultGroup;
            var mapGroup = settings.FindGroup("Scenes");
            var musicGroup = settings.FindGroup("Music");
            var soundsGroup = settings.FindGroup("Sounds");
            var modelsGroup = settings.FindGroup("Models");
            var entriesAdded = new List<AddressableAssetEntry>();
            var entriesRemoved = new List<AddressableAssetEntry>();

            var monsters = JsonUtility.FromJson<Wrapper<MonsterClassData>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/monsterclass.json"));

            //---------------------------------------------------------
            // Sprites
            //---------------------------------------------------------

            var guids = AssetDatabase.FindAssets("t:RoSpriteData", new[] { "Assets/Sprites" });
            Debug.Log($"[Addressables] Sprites found: {guids.Length}");

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var altPath = path.Replace("Imported/", "").Replace(".asset", ".spr");
                var fName = Path.GetFileNameWithoutExtension(altPath);
                Debug.Log($"[Sprite] Processing ({i + 1}/{guids.Length}) name='{fName}', sourcePath='{path}'");
                if (fName.Equals("초보자_남", StringComparison.InvariantCulture))
                {
                    Debug.LogWarning($"[Debug] ▶ 초보자_남 sprite detected at altPath: {altPath}");
                }

                if (path.Contains("BodyFemale") || path.Contains("BodyMale"))
                {
                    if (altPath.EndsWith("_0.spr") || altPath.EndsWith("_1.spr") || altPath.EndsWith("_2.spr") || altPath.EndsWith("_3.spr") ||
                        altPath.EndsWith("_4.spr") || altPath.EndsWith("_5.spr"))
                        continue;
                }

                // if (path.Contains("Imported/Weapons"))
                // {
                //     if (!path.Contains("Novice") && !path.Contains("Swordsman") && !path.Contains("Mage") && !path.Contains("Acolyte") &&
                //         !path.Contains("Merchant") && !path.Contains("Thief") && !path.Contains("Archer") && !path.Contains("Priest"))
                //     {
                //         continue;
                //     }
                // }

                if ((path.Contains("Monsters") &&
                     monsters.Items.All(m => m.SpriteName.Replace(".spr", "") != fName.ToLowerInvariant()))
                    || !path.Contains("Imported"))
                {
                    //Debug.Log("Not found: " + fName);
                    var existing = defGroup.GetAssetEntry(guids[i]);
                    if (existing == null)
                        continue;
                    Debug.Log($"[Addressables] Removing sprite entry for '{fName}'");
                    settings.RemoveAssetEntry(guids[i], true);
                    entriesRemoved.Add(existing);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guids[i], defGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                Debug.Log($"[Addressables] Creating/moving entry for '{fName}'");
                entry.address = altPath;
                Debug.Log($"[Addressables] Set address '{entry.address}' for '{fName}'");
                entry.labels.Add("Sprite");
                Debug.Log($"[Addressables] Labelled '{fName}' with [Sprite]");

                entriesAdded.Add(entry);
            }

            //---------------------------------------------------------
            // Effects
            //---------------------------------------------------------

            var effects = JsonUtility.FromJson<EffectTypeList>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/effects.json"));

            Debug.Log(effects.Effects.Count);

            guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Effects/Prefabs" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileNameWithoutExtension(path);
                if (effects.Effects.All(m => m.Name != fName))
                {
                    // Debug.Log("Not found: " + fName + " " + effects.Effects[0].Name);
                    var existing = defGroup.GetAssetEntry(guids[i]);
                    if (existing == null)
                        continue;
                    settings.RemoveAssetEntry(guids[i], true);
                    entriesRemoved.Add(existing);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guids[i], defGroup, readOnly: false, postEvent: false);
                Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                entry.labels.Add("Effect");

                entriesAdded.Add(entry);
            }

            //---------------------------------------------------------
            // Maps
            //---------------------------------------------------------

            var maps = JsonUtility.FromJson<Wrapper<ClientMapEntry>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/maps.json"));
            var musicNames = new List<string>();
            musicNames.Add("01.mp3");

            //update scenes
            guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/Maps" });
            var usedPrefabs = new HashSet<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileNameWithoutExtension(path);

                var map = maps.Items.FirstOrDefault(m => m.Code == fName);

                if (map == null)
                {
                    //Debug.Log("Not found: " + fName);
                    var existing = mapGroup.GetAssetEntry(guids[i]);
                    if (existing == null)
                        continue;
                    settings.RemoveAssetEntry(guids[i], true);
                    entriesRemoved.Add(existing);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guids[i], mapGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = path;
                entry.labels.Add("Maps");

                if (!musicNames.Contains(map.Music))
                    musicNames.Add(map.Music);

                entriesAdded.Add(entry);

                if (processModels)
                {
                    var dependencies = AssetDatabase.GetDependencies(path);
                    foreach (var d in dependencies)
                    {
                        if (!usedPrefabs.Contains(d))
                            usedPrefabs.Add(d);
                    }
                }
            }

            //update sprites
            guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Music" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileName(path);
                if (!musicNames.Contains(fName))
                {
                    //Debug.Log("Not found: " + fName);
                    var existing = musicGroup.GetAssetEntry(guids[i]);
                    if (existing == null)
                        continue;
                    settings.RemoveAssetEntry(guids[i], true);
                    entriesRemoved.Add(existing);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guids[i], musicGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                entry.labels.Add("Music");

                entriesAdded.Add(entry);
            }


            //update effect sounds
            guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds/effect" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileName(path);

                var entry = settings.CreateOrMoveEntry(guids[i], soundsGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i])
                    .Replace("Assets/Sounds/effect", "Assets/Sounds/Effects").Replace(".wav", ".ogg");
                entry.labels.Add("Sounds");

                entriesAdded.Add(entry);
            }

            //special sound cases for sounds we want to include outside of the effects folder
            guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileName(path);
                if (!Path.GetFileName(fName).StartsWith("_") &&
                    !fName.Contains("버튼소리")) //exception for title screen button lol
                    continue;

                var entry = settings.CreateOrMoveEntry(guids[i], soundsGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i])
                    .Replace("Assets/Sounds", "Assets/Sounds/Effects").Replace(".wav", ".ogg");
                entry.labels.Add("Sounds");

                entriesAdded.Add(entry);
            }

            //update sprites
            guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites/Imported/Collections" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileName(path);

                var entry = settings.CreateOrMoveEntry(guids[i], defGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                entry.labels.Add("Collections");

                entriesAdded.Add(entry);
            }

            //update sprites
            guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites/Cutins" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileName(path);

                var entry = settings.CreateOrMoveEntry(guids[i], defGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                entry.labels.Add("Cutins");

                entriesAdded.Add(entry);
            }

            guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Maps/Minimap" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileNameWithoutExtension(path).Replace("_walkmask", "");

                var map = maps.Items.FirstOrDefault(m => m.Code == fName);

                if (map == null)
                {
                    //Debug.Log("Not found: " + fName);
                    var existing = defGroup.GetAssetEntry(guids[i]);
                    if (existing == null)
                        continue;
                    settings.RemoveAssetEntry(guids[i], true);
                    entriesRemoved.Add(existing);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guids[i], defGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                entry.labels.Add("Minimap");

                entriesAdded.Add(entry);
            }


            if (processModels)
            {
                //update models
                guids = AssetDatabase.FindAssets("t:Prefab,t:Texture2D,t:Mesh,t:Material", new[] { "Assets/Models/" });

                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var fName = Path.GetFileName(path);

                    if (!usedPrefabs.Contains(path))
                    {
                        var existing = modelsGroup.GetAssetEntry(guids[i]);
                        if (existing == null)
                            continue;
                        settings.RemoveAssetEntry(guids[i], true);
                        entriesRemoved.Add(existing);

                        continue;
                    }

                    var label = GetGroupName(path);
                    var entry = settings.CreateOrMoveEntry(guids[i], modelsGroup, readOnly: false, postEvent: false);
                    //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                    entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                    entry.labels.Add(label);

                    entriesAdded.Add(entry);
                }
            }


            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, true);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entriesRemoved, true);
        }

        //[MenuItem("Ragnarok/Test Import Model")]
        static void TestImportModel()
        {
            RagnarokModelLoader.LoadModelTest();
        }

        //[MenuItem("Ragnarok/Import Test Walk Data")]
        public static RoMapData LoadWalkData(string importPath, float waterLevel)
        {
            var altitude = new RagnarokWalkableDataImporter();

            //var importPath = @"G:\Projects2\Ragnarok\Resources\data\6@tower.gat";

            var walkData = altitude.LoadWalkData(importPath, waterLevel);
            // walkData = altitude.SplitWalkData(walkData, 4, Path.GetFileNameWithoutExtension(importPath));
            walkData.name = Path.GetFileNameWithoutExtension(importPath) + "_walk";

            var saveDir = "Assets/maps/altitude";

            if (!Directory.Exists(saveDir))
                Directory.CreateDirectory(saveDir);

            var dataPath = Path.Combine(saveDir, walkData.name + ".asset").Replace("\\", "/");

            AssetDatabase.CreateAsset(walkData, dataPath);

            walkData.RefreshTextureLookup();
            walkData.RebuildAtlas();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            walkData = AssetDatabase.LoadAssetAtPath<RoMapData>(dataPath);

            return walkData;
        }

        [MenuItem("Ragnarok/Select maps to import", false, 122)]
        public static void ShowImportMapsWindow()
        {
            var window = GetWindow<RagnarokMapImporterWindow>("Import All Maps");
            window.minSize = new Vector2(300, 400);
            window.Focus();
        }

        //── Window State ─────────────────────────────────────────────────────────
        private List<ClientMapEntry> maps;
        private bool[] mapSelected;
        private Vector2 scrollPos;

        private void OnEnable()
        {
            // 1) Load maps.json
            var wrapper = JsonUtility.FromJson<Wrapper<ClientMapEntry>>(File.ReadAllText("Assets/StreamingAssets/ClientConfigGenerated/maps.json"));
            maps = wrapper.Items.ToList();

            // 2) Initialize checkboxes (true = not imported yet)
            mapSelected = new bool[maps.Count];
            for (int i = 0; i < maps.Count; i++)
            {
                var scenePath = $"Assets/Scenes/Maps/{maps[i].Code}.unity";
                mapSelected[i] = !File.Exists(scenePath);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select maps to import:", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Buttons to select or unselect all maps
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Height(20)))
            {
                for (int i = 0; i < mapSelected.Length; i++)
                    mapSelected[i] = true;
            }

            if (GUILayout.Button("Unselect All", GUILayout.Height(20)))
            {
                for (int i = 0; i < mapSelected.Length; i++)
                    mapSelected[i] = false;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Scrollable list of maps
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int i = 0; i < maps.Count; i++)
            {
                mapSelected[i] = EditorGUILayout.ToggleLeft(
                    $"{maps[i].Name}  ({maps[i].Code})",
                    mapSelected[i]
                );
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Import Selected Maps", GUILayout.Height(30)))
            {
                ImportSelectedMaps();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Remove Selected Maps", GUILayout.Height(30)))
            {
                CleanSelectedMaps();
            }
        }

        private void ImportSelectedMaps()
        {
            // Ensure maps folder exists
            if (!Directory.Exists("Assets/Scenes/Maps/"))
                Directory.CreateDirectory("Assets/Scenes/Maps/");

            int total = mapSelected.Count(x => x);
            if (total == 0)
            {
                Debug.Log("No maps selected for import.");
                return;
            }

            int done = 0;
            for (int i = 0; i < maps.Count; i++)
            {
                if (!mapSelected[i]) continue;

                var map = maps[i];
                var code = map.Code;
                var gnd = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, code + ".gnd");

                EditorUtility.DisplayProgressBar(
                    "Importing Maps",
                    $"Importing {map.Name} ({done + 1}/{total})…",
                    (float)done / total
                );

                try
                {
                    // Pre-clean any leftover assets from a previous crash
                    DeleteExistingMapAssets(code);

                    // Ensure file exists
                    if (!File.Exists(gnd))
                        throw new FileNotFoundException($"Map file not found: {gnd}");

                    // Do the actual import
                    ImportMap(gnd);
                }
                catch (Exception ex)
                {
                    // Abort on first error
                    Debug.LogError($"[Map Import] Aborting import of {map.Name} ({code}): {ex}\n{ex.StackTrace}");
                    EditorUtility.ClearProgressBar();

                    // Cleanup any partial assets
                    DeleteExistingMapAssets(code);
                    return;
                }

                done++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Imported {done} map(s).");
        }

        // ─── Delete all generated assets for each selected map ────────────────────
        private void CleanSelectedMaps()
        {
            int cleaned = 0;
            for (int i = 0; i < maps.Count; i++)
            {
                if (!mapSelected[i]) continue;
                DeleteExistingMapAssets(maps[i].Code);
                cleaned++;
            }

            // Make sure Unity’s database fully picks up the deletions
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Map Cleanup] Removed assets for {cleaned} selected map(s).");
        }

// ─── DeleteExistingMapAssets ──────────────────────────────────────────────
        private static void DeleteExistingMapAssets(string code)
        {
            // 1) Delete any scene assets in Assets/Scenes/Maps
            foreach (var guid in AssetDatabase.FindAssets(code, new[] { "Assets/Scenes/Maps" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.DeleteAsset(path))
                    Debug.Log($"[Map Cleanup] Deleted scene {path}");
            }

            // 2) Delete *all* files under Assets/Maps that start with "{code}"
            var mapsRoot = "Assets/Maps";
            if (Directory.Exists(mapsRoot))
            {
                var absPaths = Directory.GetFiles(mapsRoot, $"{code}*", SearchOption.AllDirectories);
                foreach (var abs in absPaths)
                {
                    // Convert absolute to “Assets/…” relative
                    var rel = abs.Replace("\\", "/");
                    var idx = rel.IndexOf("Assets/");
                    if (idx >= 0) rel = rel.Substring(idx);

                    if (AssetDatabase.DeleteAsset(rel))
                        Debug.Log($"[Map Cleanup] Deleted map asset {rel}");
                }
            }

            // 3) Delete any generated prefabs under Assets/Models/Prefabs that start with "{code}"
            var prefabRoot = "Assets/Models/Prefabs";
            if (Directory.Exists(prefabRoot))
            {
                // use AssetDatabase.FindAssets so we catch .prefab + .prefab.meta
                foreach (var guid in AssetDatabase.FindAssets($"{code} t:Prefab", new[] { prefabRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.DeleteAsset(path))
                        Debug.Log($"[Map Cleanup] Deleted prefab {path}");
                }
            }
        }


        //[MenuItem("Ragnarok/Import Water", false, 123)]
        public static void ImportWater()
        {
            var waterDir = Path.Combine(Application.dataPath, "Maps/Texture/Water/").Replace("\\", "/");
            //Debug.Log(waterDir);

            var baseFolder = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "texture\\워터").Replace("\\", "/");
            //Debug.Log(baseFolder);

            if (!Directory.Exists(waterDir))
                Directory.CreateDirectory(waterDir);

            var cnt = 0;

            foreach (var path in Directory.GetFiles(baseFolder, "*.jpg"))
            {
                //Debug.Log(path);
                var outdir = Path.Combine(waterDir, Path.GetFileName(path));
                if (!File.Exists(outdir))
                {
                    File.Copy(path, outdir);
                    cnt++;
                }
            }

            if (cnt > 0)
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        static void ImportMap(string f)
        {
            var saveDir = Path.Combine(Application.dataPath, "maps").Replace("\\", "/");

            var lastDirectory = Path.GetDirectoryName(f);
            var baseName = Path.GetFileNameWithoutExtension(f);
            Debug.Log(f);

            ImportWater();

            var relativeDir = saveDir.Substring(saveDir.IndexOf("Assets/"));

            var loader = new RagnarokMapLoader();
            var data = loader.ImportMap(f, relativeDir);
            var dataPath = Path.Combine(relativeDir, data.name + ".asset").Replace("\\", "/");

            AssetDatabase.CreateAsset(data, dataPath);

            Debug.Log($"[ImportMap] Created RoMapData asset at {dataPath}");
            data = AssetDatabase.LoadAssetAtPath<RoMapData>(dataPath);

            Debug.Log("[ImportMap] RefreshTextureLookup()");
            data.RefreshTextureLookup();
            Debug.Log("[ImportMap] RebuildAtlas()");
            data.RebuildAtlas();

            var gatPath = Path.Combine(lastDirectory, baseName + ".gat");
            RoWater water = null;

            var resourcePath = Path.Combine(lastDirectory, baseName + ".rsw");
            if (File.Exists(resourcePath))
            {
                Debug.Log("[ImportMap] Loading world RSW");
                var world = RagnarokResourceLoader.LoadResourceFile(resourcePath, data);
                world.name = baseName + " world data";
                water = world.Water;

                var worldFolder = Path.Combine(relativeDir, "world");
                if (!Directory.Exists(worldFolder))
                    Directory.CreateDirectory(worldFolder);

                var worldAssetPath = Path.Combine(worldFolder, baseName + "_world.asset").Replace("\\", "/");

                Debug.Log("[ImportMap] World loaded; saving world.asset");
                AssetDatabase.CreateAsset(world, worldAssetPath);

                foreach (var model in world.Models)
                {
                    var baseFolder = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "model");

                    var modelPath = Path.Combine(baseFolder, model.FileName);
                    var relative = DirectoryHelper.GetRelativeDirectory(baseFolder, Path.GetDirectoryName(modelPath));
                    var mBaseName = Path.GetFileNameWithoutExtension(model.FileName);

                    var prefabFolder = Path.Combine("Assets/models/prefabs/", relative).Replace("\\", "/");
                    var prefabPath = Path.Combine(prefabFolder, mBaseName + ".prefab").Replace("\\", "/");

                    if (!Directory.Exists(prefabFolder))
                        Directory.CreateDirectory(prefabFolder);


                    if (!File.Exists(prefabPath))
                    {
                        try
                        {
                            var modelLoader = new RagnarokModelLoader();

                            modelLoader.LoadModel(modelPath, relative);
                            var obj = modelLoader.Compile();

                            PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);

                            var prefabRef = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            DestroyImmediate(obj);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Failed to load model {Path.GetFileName(modelPath)}! Exception:\r\n{e}");
                        }
                    }
                }

                //var builder = new RagnarokWorldSceneBuilder();
                //builder.Load(data, world);
            }

            var waterLevel = -water.Level + (water.WaveHeight / 5f) - 0.01f;
            Debug.Log("[ImportMap] LoadWalkData()");
            data.WalkData = LoadWalkData(gatPath, waterLevel);
            Debug.Log("[ImportMap] WalkData loaded at " + data.WalkData.name);

            AssetDatabase.SaveAssets();

            EditorUtility.UnloadUnusedAssetsImmediate();

            Debug.Log("[ImportMap] About to call importer.Import()");
            var importer = new RagnarokMapDataImporter(dataPath, baseName);
            importer.Import(true, true, true, true, true);
            Debug.Log("[ImportMap] importer.Import() FINISHED");

            AssetDatabase.SaveAssets();
        }
        //
        // [MenuItem("Ragnarok/Import THE Map")]
        // static void ImportFiles2()
        // {
        //     ImportMap(Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "pay_dun00.gnd"));
        //
        // }


        [MenuItem("Ragnarok/Import Maps", false, 121)]
        static void ImportFiles()
        {
            var files = StandaloneFileBrowser.OpenFilePanel("Open File", RagnarokDirectory.GetRagnarokDataDirectory,
                "gnd", true);

            if (files.Length <= 0)
                return;

            //var saveDir = EditorUtility.SaveFolderPanel("Save Folder", Application.dataPath, "");
            var saveDir = Path.Combine(Application.dataPath, "maps").Replace("\\", "/");
            //Debug.Log(saveDir);

            foreach (var f in files)
            {
                try
                {
                    ImportMap(f);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception generated while importing map {f}!");
                    Debug.LogException(e);
                }
            }
        }

        //[MenuItem("Ragnarok/Test Import Texture")]
        //static void TestImportTexture()
        //{
        //    //var file = EditorUtility.OpenFilePanel("Load Image", )
        //    var tex = LoadTexture(@"G:\Projects2\Ragnarok\Resources\data\texture\기타마을\sage_bar01.bmp");
        //    var png = tex.EncodeToPNG();
        //    File.WriteAllBytes(@"G:\Projects2\test.png", png);
        //}

        private static Texture2D LoadTexture(string path)
        {
            var bmp = new BMPLoader();
            bmp.ForceAlphaReadWhenPossible = false;
            var img = bmp.LoadBMP(path);

            var colors = (Color32[])img.imageData.Clone();

            var width = img.info.width;
            var height = img.info.height;

            //magic pink conversion and transparent color expansion
            for (var y = 0; y < width; y++)
            {
                for (var x = 0; x < height; x++)
                {
                    var count = 0;
                    var r = 0;
                    var g = 0;
                    var b = 0;

                    var color = colors[x + y * width];
                    if (color.r != 255 || color.g != 0 || color.b != 255)
                        continue;

                    //Debug.Log("OHWOW: " + color);

                    for (var y2 = -1; y2 <= 1; y2++)
                    {
                        for (var x2 = -1; x2 <= 1; x2++)
                        {
                            if (y + y2 < 0 || y + y2 >= width)
                                continue;
                            if (x + x2 < 0 || x + x2 >= height)
                                continue;
                            var color2 = colors[x + x2 + (y + y2) * width];

                            if (color2.r == 255 && color2.g == 0 && color2.b == 255)
                                continue;

                            count++;

                            r += color2.r;
                            g += color2.g;
                            b += color2.b;
                        }
                    }

                    if (count > 0)
                    {
                        var r2 = (byte)Mathf.Clamp(r / count, 0, 255);
                        var g2 = (byte)Mathf.Clamp(g / count, 0, 255);
                        var b2 = (byte)Mathf.Clamp(b / count, 0, 255);

                        //Debug.Log($"{x},{y} - change {color} to {r2},{g2},{b2}");

                        img.imageData[x + y * width] = new Color32(r2, g2, b2, 0);
                    }
                    else
                        img.imageData[x + y * width] = new Color32(0, 0, 0, 0);
                }
            }

            return img.ToTexture2D();
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Environment;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Effects.EffectHandlers.Skills.Assassin;
using Assets.Scripts.Effects.EffectHandlers.Skills.Priest;
using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Sprites
{
    public class MapViewpoint
    {
        public string MapName;
        public int ZoomMin;
        public int ZoomDist;
        public int ZoomIn;
        public int SpinMin;
        public int SpinMax;
        public int SpinIn;
        public int HeightMin;
        public int HeightMax;
        public int HeightIn;
    }

    public class ClientDataLoader : MonoBehaviour
    {
        public static ClientDataLoader Instance;
        public static event Action InitializationCompleted;

        public SpriteAtlas ItemIconAtlas;
        public Sprite ShadowSprite;

        public int ServerVersion;

        public readonly Dictionary<int, MonsterClassData> MonsterClassLookup = new();
        public readonly Dictionary<int, PlayerHeadData> PlayerHeadLookup = new();
        public readonly Dictionary<int, PlayerClassData> PlayerClassLookup = new();
        public readonly Dictionary<int, Dictionary<int, PlayerWeaponData>> PlayerWeaponLookup = new();
        public readonly Dictionary<int, WeaponClassData> WeaponClassData = new();
        public readonly Dictionary<CharacterSkill, SkillData> SkillData = new();
        public readonly Dictionary<string, MapViewpoint> MapViewpoints = new();
        public readonly Dictionary<string, Dictionary<CharacterSkill, UniqueAttackAction>> UniqueSpriteActions = new();
        public readonly Dictionary<string, MetamorphTransitionResult> MetamorphTransitionResult = new();
        public readonly Dictionary<int, ClientSkillTree> JobSkillTrees = new();
        public readonly Dictionary<string, int> JobNameToIdTable = new();
        public readonly Dictionary<int, ItemData> ItemIdLookup = new();
        public readonly Dictionary<string, ItemData> ItemNameLookup = new();
        public readonly Dictionary<string, ClientMapEntry> MapDataLookup = new();
        public readonly Dictionary<string, string> DisplaySpriteList = new();
        public readonly Dictionary<string, string> ItemDescriptionTable = new();
        public readonly Dictionary<int, CardPrefixData> CardPrefixPostfixTable = new();
        public readonly Dictionary<int, EmoteData> EmoteDataTable = new();
        public readonly Dictionary<int, StatusEffectData> StatusEffectData = new();
        public readonly int[] JobExpData = new int[70 * 3];
        private readonly int[] baseExpData = new int[99];

        private readonly List<string> validMonsterClasses = new();
        private readonly List<string> validMonsterCodes = new();

        private const string MonsterClassDataPath = "ClientConfigGenerated/monsterclass.json";
        private const string PlayerClassDataPath = "ClientConfigGenerated/playerclass.json";
        private const string PlayerHeadDataPath = "ClientConfig/headdata.json";
        private const string PlayerWeaponDataPath = "ClientConfigGenerated/jobweaponinfo.json";
        private const string WeaponClassDataPath = "ClientConfigGenerated/weaponclass.json";
        private const string SkillDataPath = "ClientConfigGenerated/skillinfo.json";
        private const string SkillTreeDataPath = "ClientConfigGenerated/skilltree.json";
        private const string MapViewpointDataPath = "ClientConfig/MapViewpointList.txt";
        private const string UniqueAttackActionDataPath = "ClientConfig/monsteractions.json";
        private const string MetamorphResultDataPath = "ClientConfig/metamorph_actions.json";
        private const string ItemDataPath = "ClientConfigGenerated/items.json";
        private const string MapDataPath = "ClientConfigGenerated/maps.json";
        private const string EquipmentSpriteDataPath = "ClientConfigGenerated/displaySpriteTable.txt";
        private const string ItemDescDataPath = "ClientConfigGenerated/itemDescriptions.json";
        private const string CardPrefixDataPath = "ClientConfigGenerated/cardprefixes.json";
        private const string ServerVersionDataPath = "ClientConfigGenerated/ServerVersion.txt";
        private const string PatchNoteDataPath = "ClientConfigGenerated/PatchNotes.txt";
        private const string JobExpDataPath = "ClientConfigGenerated/jobexpchart.txt";
        private const string BaseExpDataPath = "ClientConfigGenerated/levelchart.txt";
        private const string EmoteDataPath = "ClientConfigGenerated/emotes.json";
        private const string StatusEffectDataPath = "ClientConfigGenerated/statusinfo.json";

// #if UNITY_WEBGL
        private string[] streamingAssets = new[]
        {
            "ClientConfigGenerated/effects.json",
            "ClientConfig/AdminWarpList.txt",
            "ClientConfig/fogData.json",
            MapDataPath,
            // Client database window data; loaded on-demand at runtime so they must stay resident (not init-only).
            "ClientConfigGenerated/monsterdatabase.json",
            "ClientConfigGenerated/npcdatabase.json",
            "ClientConfigGenerated/mapwarps.json",
        };

        private string[] initializeOnlyStreamingAssets = new[]
        {
            MonsterClassDataPath,
            PlayerClassDataPath,
            PlayerHeadDataPath,
            PlayerWeaponDataPath,
            WeaponClassDataPath,
            SkillDataPath,
            SkillTreeDataPath,
            MapViewpointDataPath,
            UniqueAttackActionDataPath,
            MetamorphResultDataPath,
            ItemDataPath,
            // MapDataPath,
            EquipmentSpriteDataPath,
            ItemDescDataPath,
            CardPrefixDataPath,
            ServerVersionDataPath,
            PatchNoteDataPath,
            JobExpDataPath,
            BaseExpDataPath,
            EmoteDataPath,
            StatusEffectDataPath,
        };

        private Dictionary<string, string> streamingAssetsData;
// #endif

        private bool isInitialized;
        public bool IsInitialized => isInitialized;

        private readonly Dictionary<CharacterSkill, string> playerCastNameCache = new();
        private readonly Dictionary<CharacterSkill, string> monsterCastNameCache = new();

        public Sprite GetIconAtlasSprite(string name) => EffectSharedMaterialManager.GetAtlasSprite(ItemIconAtlas, name);

        // private static int EffectClassId = 3999;

        public bool IsValidMonsterName(string name) => validMonsterClasses.Contains(name);
        public bool IsValidMonsterCode(string name) => validMonsterCodes.Contains(name);

        public int GetJobIdForName(string name) => JobNameToIdTable.GetValueOrDefault(name, -1);
        public string GetFullNameForMap(string mapName) => MapDataLookup.TryGetValue(mapName, out var map) ? map.Name : "Unknown Map";
        public string GetJobNameForId(int id) => PlayerClassLookup.TryGetValue(id, out var job) ? job.Name : "-";
        public string GetSkillName(CharacterSkill skill) => SkillData.TryGetValue(skill, out var skOut) ? skOut.Name : "";

        public string GetPlayerSkillCastName(CharacterSkill skill)
        {
            if (!playerCastNameCache.TryGetValue(skill, out var text))
                playerCastNameCache[skill] = text = GetSkillName(skill) + "!!";
            return text;
        }

        public string GetMonsterSkillCastName(CharacterSkill skill)
        {
            if (!monsterCastNameCache.TryGetValue(skill, out var text))
                monsterCastNameCache[skill] = text = "<size=-2><color=#FF8888>" + GetSkillName(skill) + "</size>";
            return text;
        }
        public SkillData GetSkillData(CharacterSkill skill) => SkillData[skill];
        public SkillTarget GetSkillTarget(CharacterSkill skill) => SkillData.TryGetValue(skill, out var target) ? target.Target : SkillTarget.Any;
        public Dictionary<CharacterSkill, SkillData> GetAllSkills() => SkillData;
        public ClientSkillTree GetSkillTree(int jobId) => JobSkillTrees.GetValueOrDefault(jobId);
        public Dictionary<int, EmoteData> GetEmoteTable => EmoteDataTable;

        public MapViewpoint GetMapViewpoint(string mapName) => MapViewpoints.GetValueOrDefault(mapName);
        public ClientMapEntry GetMapInfo(string mapName) => MapDataLookup.GetValueOrDefault(mapName);
        public MonsterClassData GetMonsterData(int classId) => MonsterClassLookup.GetValueOrDefault(classId);

        //monsters below class id 4000 keep their sprites in the npc folder
        public static string GetMonsterSpritePath(int classId, string spriteName) =>
            (classId < 4000 ? "Assets/Sprites/Npcs/" : "Assets/Sprites/Monsters/") + spriteName;
        public ItemData GetItemById(int id) => ItemIdLookup.TryGetValue(id, out var item) ? item : ItemIdLookup[-1];
        public ItemData GetItemByName(string name) => ItemNameLookup[name];
        public bool TryGetItemByName(string name, out ItemData item) => ItemNameLookup.TryGetValue(name, out item);
        public bool TryGetItemById(int id, out ItemData item) => ItemIdLookup.TryGetValue(id, out item);
        public string GetItemDescription(string itemCode) => ItemDescriptionTable.GetValueOrDefault(itemCode, "No description available.");
        public CardPrefixData GetCardPrefixData(int id) => CardPrefixPostfixTable.GetValueOrDefault(id, null);
        public StatusEffectData GetStatusEffect(int id) => StatusEffectData.GetValueOrDefault(id, null);

        //-1 at the highest level
        public int GetBaseExpRequired(int level) => level < 1 || level >= 99 ? -1 : baseExpData[level - 1];

        //-1 at the job's highest level
        public int GetJobExpRequired(int job, int level)
        {
            if (!PlayerClassLookup.TryGetValue(job, out var jobInfo))
                return -1;
            if (level < 0 || level >= jobInfo.MaxJobLevel || level >= 70)
                return -1;
            return JobExpData[jobInfo.ExpChart * 70 + level];
        }

        public bool TryGetMetamorphResult(string spriteName, out MetamorphTransitionResult result) =>
            MetamorphTransitionResult.TryGetValue(spriteName, out result);

        public static int UniqueItemStartId = 20000;
        public string LatestPatchNotes = "";
        public string PatchNotes;

        public string GetHitSoundForWeapon(int weaponId)
        {
            if (!WeaponClassData.TryGetValue(weaponId, out var weapon))
                weapon = WeaponClassData[0];

            var hitSoundsCount = weapon.HitSounds.Count;
            if (hitSoundsCount <= 1)
                return weapon.HitSounds[0];

            return weapon.HitSounds[Random.Range(0, hitSoundsCount)];
        }

        public bool GetUniqueAction(string spriteName, CharacterSkill skill, out UniqueAttackAction actOut)
        {
            actOut = null;
            if (UniqueSpriteActions.TryGetValue(spriteName, out var list))
                if (list.TryGetValue(skill, out var action))
                    actOut = action;
            return actOut != null;
        }

        private void Awake()
        {
            Instance = this;
            //Initialize();
        }

        public IEnumerator LoadStreamingAssets()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer || Application.isEditor)
            {
                streamingAssetsData = new Dictionary<string, string>();

                var assetList = new List<string>();
                assetList.AddRange(streamingAssets);
                assetList.AddRange(initializeOnlyStreamingAssets);
                
                var bom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

                foreach (var assetName in assetList)
                {
                    var path = Path.Combine(Application.streamingAssetsPath, assetName);
                    if (path.StartsWith("/")) //windows gives C:/..., which works as is. linux gives /home/..., which needs file:// in front
                        path = "file://" + path;
                    using UnityWebRequest www = UnityWebRequest.Get(path);

                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        var txt = www.downloadHandler.text;
                        if (txt.StartsWith(bom, StringComparison.Ordinal))
                            txt = txt.Remove(0, bom.Length); //fix byte order mark because unity is fucking stupid

                        streamingAssetsData.Add(assetName, txt);
                    }
                    else
                    {
                        Debug.LogError($"Could not load streaming asset using WWW request at path: {path}");
                        streamingAssetsData.Add(assetName, "");
                    }
                }
            }
        }

        public static string ReadStreamingAssetFile(string file)
        {
#if UNITY_EDITOR
            if (!Instance.streamingAssetsData.ContainsKey(file))
                Debug.LogWarning($"Warning, attempting to load streaming asset {file}, but it is not in the streamingAssets list in ClientDataLoader.cs."
                                 + " This can cause the file to be unloadable in a webGL build.");
#endif

#if !UNITY_WEBGL && !UNITY_EDITOR && !UNITY_ANDROID
            //Non WebGL platforms can read from streaming assets directly.
            return File.ReadAllText(Path.Combine(Application.streamingAssetsPath, file));
#endif

            if (Instance.streamingAssetsData.TryGetValue(file, out var data))
                return data;

            Debug.LogWarning($"Could not load streaming asset from memory for path: {file}");
            if (Application.isEditor)
                return File.ReadAllText(Path.Combine(Application.streamingAssetsPath, file));

            return "";
        }

        public void Initialize()
        {
            if (isInitialized && Instance != null)
                return;
            Instance = this;

            ServerVersion = int.Parse(ReadStreamingAssetFile(ServerVersionDataPath));

            using var jobStr = new StringReader(ReadStreamingAssetFile(JobExpDataPath));
            var jobLvl = 0;
            while (true)
            {
                var line = jobStr.ReadLine();
                if (line == null)
                    break;
                var s = line.Split(",");
                JobExpData[jobLvl] = int.Parse(s[0]);
                JobExpData[70 + jobLvl] = int.Parse(s[1]);
                JobExpData[140 + jobLvl] = int.Parse(s[2]);
                jobLvl++;
            }

            var baseExpLines = ReadStreamingAssetFile(BaseExpDataPath).Split("\n");
            for (var i = 0; i < baseExpData.Length; i++)
                baseExpData[i] = int.Parse(baseExpLines[i].Trim());

            var entityData = JsonUtility.FromJson<Wrapper<MonsterClassData>>(ReadStreamingAssetFile(MonsterClassDataPath));
            foreach (var m in entityData.Items)
            {
                MonsterClassLookup.Add(m.Id, m);
                validMonsterClasses.Add(m.Name);
                validMonsterCodes.Add(m.Code);
            }

            var headData = JsonUtility.FromJson<Wrapper<PlayerHeadData>>(ReadStreamingAssetFile(PlayerHeadDataPath));
            foreach (var h in headData.Items)
            {
                //playerHeadLookup.Add(h.Id, h);

                for (var i = 0; i < Mathf.Min(h.FemaleIds.Length, h.MaleIds.Length); i++)
                {
                    var colorHead = h.Id + (i << 8);

                    var mPath = h.SpriteMale.Replace("<id>", h.MaleIds[i]);
                    var fPath = h.SpriteFemale.Replace("<id>", h.FemaleIds[i]);

// #if UNITY_WEBGL
//                     Debug.Log(mPath);
//                     Debug.Log(fPath);
// #else
//                     var handle = Addressables.LoadResourceLocationsAsync(mPath);
//                     handle.WaitForCompletion();
//                     var handle2 = Addressables.LoadResourceLocationsAsync(fPath);
//                     handle2.WaitForCompletion();
// #endif
                    if (AssetExists(mPath) && AssetExists(fPath))
                    {
                        PlayerHeadLookup.Add(colorHead, new PlayerHeadData()
                        {
                            Id = colorHead,
                            SpriteMale = mPath,
                            SpriteFemale = fPath
                        });
                    }
                    else
                        Debug.LogWarning($"Failed to find expected head variation {h.MaleIds[i]} and {h.FemaleIds[i]}");
                }
            }


            var playerData = JsonUtility.FromJson<Wrapper<PlayerClassData>>(ReadStreamingAssetFile(PlayerClassDataPath));
            foreach (var p in playerData.Items)
            {
                PlayerClassLookup.Add(p.Id, p);
                JobNameToIdTable.Add(p.Name, p.Id);
            }

            //split weapon entries into a tree of base job -> weapon class -> sprite list
            var weaponData = JsonUtility.FromJson<Wrapper<PlayerWeaponData>>(ReadStreamingAssetFile(PlayerWeaponDataPath));
            foreach (var weapon in weaponData.Items)
            {
                if (!PlayerWeaponLookup.ContainsKey(weapon.Job))
                    PlayerWeaponLookup.Add(weapon.Job, new Dictionary<int, PlayerWeaponData>());

                var w = weapon.Class;
                if (weapon.Class2 > 0)
                    w += weapon.Class2 << 8;

                var jList = PlayerWeaponLookup[weapon.Job];
                jList.TryAdd(w, weapon);
            }

            var weaponClass = JsonUtility.FromJson<Wrapper<WeaponClassData>>(ReadStreamingAssetFile(WeaponClassDataPath));
            foreach (var weapon in weaponClass.Items)
                WeaponClassData.TryAdd(weapon.Id, weapon);

            var emoteData = JsonUtility.FromJson<Wrapper<EmoteData>>(ReadStreamingAssetFile(EmoteDataPath));
            foreach (var emote in emoteData.Items)
                if (!EmoteDataTable.TryAdd(emote.Id, emote))
                    Debug.LogWarning($"Failed to add emote {emote.Id} to emote table");

            var items = JsonUtility.FromJson<Wrapper<ItemData>>(ReadStreamingAssetFile(ItemDataPath));

            var itemIcons = new Dictionary<string, string>();
            foreach (var item in items.Items)
            {
                ItemIdLookup.Add(item.Id, item);
                ItemNameLookup.Add(item.Code, item);
                if (!itemIcons.ContainsKey(item.Sprite))
                    itemIcons.Add(item.Sprite, item.Code);
                item.Sprite = itemIcons[item.Sprite];
            }

            ItemIdLookup.Add(-1, new ItemData()
            {
                Code = "UNKNOWN_ITEM",
                Id = -1,
                IsUnique = true,
                ItemClass = ItemClass.Etc,
                Name = "Unknown Item",
                Sprite = "Apple"
            });

            foreach (var l in Regex.Split(ReadStreamingAssetFile(EquipmentSpriteDataPath), "\n|\r|\r\n"))
            {
                if (string.IsNullOrWhiteSpace(l))
                    continue;
                // Debug.Log(l);
                var l2 = l.Trim().Split('\t');
                DisplaySpriteList.Add(l2[0], l2[1]);
            }

            var uniqueAttacks = JsonUtility.FromJson<Wrapper<UniqueAttackAction>>(ReadStreamingAssetFile(UniqueAttackActionDataPath));
            foreach (var action in uniqueAttacks.Items)
            {
                if (!UniqueSpriteActions.TryGetValue(action.Sprite, out var list))
                {
                    list = new Dictionary<CharacterSkill, UniqueAttackAction>();
                    UniqueSpriteActions.Add(action.Sprite, list);
                }

                if (Enum.TryParse(action.Action, out CharacterSkill skill))
                    list.Add(skill, action);
                else
                    Debug.LogWarning($"Could not convert {action.Action} to a skill type when parsing unique skill actions");
            }

            var json = ReadStreamingAssetFile(MetamorphResultDataPath); //File.ReadAllText(Path.Combine(Application.streamingAssetsPath, MetamorphResultDataPath)); // ;
            var metamorphResults = JsonUtility.FromJson<Wrapper<MetamorphTransitionResult>>(json);
            foreach (var result in metamorphResults.Items)
            {
                MetamorphTransitionResult.Add(result.Sprite, result);
            }

            var skills = JsonUtility.FromJson<Wrapper<SkillData>>(ReadStreamingAssetFile(SkillDataPath));
            foreach (var skill in skills.Items)
            {
                skill.Icon = "skill_" + skill.Icon;
                skill.DescEn = skill.DescEn.Replace("\r\n", "\n");
                skill.DescEn = skill.DescEn.Replace("<br>\n", "<br>"); //should do this elsewhere honestly...
                skill.DescEn = skill.DescEn.Replace("\n", "<line-height=25>\n</line-height>");
                SkillData.Add(skill.SkillId, skill);
            }

            var trees = JsonUtility.FromJson<Wrapper<ClientSkillTree>>(ReadStreamingAssetFile(SkillTreeDataPath));
            foreach (var tree in trees.Items)
                JobSkillTrees.Add(tree.ClassId, tree);

            var prePosData = JsonUtility.FromJson<Wrapper<CardPrefixData>>(ReadStreamingAssetFile(CardPrefixDataPath));
            foreach (var dat in prePosData.Items)
                CardPrefixPostfixTable.Add(dat.Id, dat);

            foreach (var mapDef in ReadStreamingAssetFile(MapViewpointDataPath).Split("\r\n"))
            {
                var s = mapDef.Split(',');
                if (s.Length < 9 || s[0] == "map")
                    continue;
                MapViewpoints.Add(s[0], new MapViewpoint()
                {
                    MapName = s[0],
                    ZoomMin = int.Parse(s[1]),
                    ZoomDist = int.Parse(s[2]),
                    ZoomIn = int.Parse(s[3]),
                    SpinMin = int.Parse(s[4]),
                    SpinMax = int.Parse(s[5]),
                    SpinIn = int.Parse(s[6]),
                    HeightMin = int.Parse(s[7]),
                    HeightMax = int.Parse(s[8]),
                    HeightIn = int.Parse(s[9]),
                });
            }

            var mapClass = JsonUtility.FromJson<Wrapper<ClientMapEntry>>(ReadStreamingAssetFile(MapDataPath));
            foreach (var map in mapClass.Items)
                MapDataLookup.TryAdd(map.Code, map);

            var statusData = JsonUtility.FromJson<Wrapper<StatusEffectData>>(ReadStreamingAssetFile(StatusEffectDataPath));
            foreach (var status in statusData.Items)
                StatusEffectData.Add((int)status.StatusEffect, status);

            var itemDescriptions = JsonUtility.FromJson<Wrapper<ItemDescription>>(ReadStreamingAssetFile(ItemDescDataPath));
            foreach (var desc in itemDescriptions.Items)
                ItemDescriptionTable.Add(desc.Code, desc.Description);

            var notes = JsonUtility.FromJson<Wrapper<PatchNotes>>(ReadStreamingAssetFile(PatchNoteDataPath));

            var sb = new StringBuilder();
            //sb.AppendLine("<b>Changes</b>");
            if (notes.Items.Length > 0)
                LatestPatchNotes = notes.Items[0].Date;
            for (var i = 0; i < notes.Items.Length && i < 5; i++)
            {
                sb.AppendLine($"<b><u><size=+2>{notes.Items[i].Date}</size></u></b>");
                sb.AppendLine(notes.Items[i].Desc);
                sb.Append("\n");
            }

            PatchNotes = sb.ToString();

            if (streamingAssetsData != null)
            {
                foreach (var asset in initializeOnlyStreamingAssets)
                    streamingAssetsData.Remove(asset); //no longer necessary
            }

            var builder = gameObject.GetComponent<EntityBuilder>();
            builder.Initialize();
            
            isInitialized = true;
            InitializationCompleted?.Invoke();
        }

        public string GetPlayerBodySpriteName(int jobId, bool isMale)
        {
            var pData = PlayerClassLookup[0]; //novice
            if (PlayerClassLookup.TryGetValue(jobId, out var lookupData))
                pData = lookupData;
            else
                Debug.LogWarning("Failed to find player with id of " + jobId);

            return isMale ? pData.SpriteMale : pData.SpriteFemale;
        }

        public string GetPlayerHeadSpriteName(int headId, int color, bool isMale)
        {
            var hData = PlayerHeadLookup[0]; //default;
            if (PlayerHeadLookup.TryGetValue(headId + (color << 8), out var lookupData2)) //see if we have a head with the right palette
                hData = lookupData2;
            else if (PlayerHeadLookup.TryGetValue(headId, out lookupData2)) //fallback to default color
                hData = lookupData2;

            return isMale ? hData.SpriteMale : hData.SpriteFemale;
        }

        public string GetHeadgearSpriteName(int itemId, bool isMale)
        {
            if (!ItemIdLookup.TryGetValue(itemId, out var item))
            {
                Debug.LogWarning($"Failed to find headgear with itemId {itemId}");
                return null;
            }

            if (!DisplaySpriteList.TryGetValue(item.Code, out var hatSprite))
            {
                Debug.LogWarning($"Failed to find headgear with itemId {itemId}");
                return null;
            }

            return $"Assets/Sprites/Headgear/{(isMale ? "Male/남_" : "Female/여_")}{hatSprite}.spr";
        }

        public static bool AssetExists(object key)
        {
            if (Application.isPlaying)
            {
#if !UNITY_EDITOR && UNITY_WEBGL
                if (NetworkManager.ResourceLocator.Locate(key, null, out _))
                    return true;
#else
                foreach (var l in Addressables.ResourceLocators)
                {
                    IList<IResourceLocation> locs;
                    if (l.Locate(key, null, out locs))
                        return true;
                }
#endif
                return false;
            }
            else if (Application.isEditor && !Application.isPlaying)
            {
#if UNITY_EDITOR
                // note: my keys are always asset file paths
                return File.Exists(Path.Combine(Application.dataPath, (string)key));
#endif
            }

            return false;
        }

        public static bool DoesAddressableExist<T>(string key)
        {
            foreach (var local in Addressables.ResourceLocators)
            {
                local.Locate(key, typeof(T), out IList<IResourceLocation> resourceLocations);
                if (resourceLocations != null)
                {
                    if (resourceLocations.Count >= 2)
                        Debug.LogWarning($"key = {key} type = {typeof(T).Name} was found in {resourceLocations.Count} locations.");
                    //location = resourceLocations[0];
                    return true;
                }
            }

            Debug.LogWarning($"Addressable {key} not found.");

            return false;
        }

        public void LoadAndAttachWeapon(ServerControllable ctrl, int item, int offHand = 0)
        {
            var weaponSpriteFile = "";
            var isEffect = item == int.MaxValue;
            var weaponClass = ctrl.WeaponClass;
            if (offHand > 0)
            {
                if (weaponClass == 0)
                    weaponClass = offHand; //if we only have a weapon in our offhand, we use the single hand variant
                else
                    weaponClass += offHand << 8;
            }

            var attachPosition = isEffect ? EquipPosition.Accessory : EquipPosition.Weapon; //it's not an accessory but too lazy to make a new option
            GameObject oldObj = null;
            if (ctrl.AttachedComponents.TryGetValue(attachPosition, out var existing))
            {
                ctrl.SpriteAnimator.ChildrenSprites.Remove(existing.GetComponent<RoSpriteAnimator>());
                ctrl.AttachedComponents.Remove(attachPosition);
                oldObj = existing;
            }

            if (ctrl.WeaponClass == 0)
            {
                Destroy(oldObj);
                if (!isEffect)
                {
                    if (PlayerWeaponLookup.TryGetValue(ctrl.OverrideClassId, out var weaponsByJob2))
                        if (weaponsByJob2.TryGetValue(0, out var unarmed))
                            ctrl.SpriteAnimator.PreferredAttackMotion = ctrl.IsMale ? unarmed.AttackMale : unarmed.AttackFemale;
                    LoadAndAttachWeapon(ctrl, int.MaxValue);
                }
                return;
            }

            var data = GetItemById(item);
            if (offHand == 0 && data.Id > 0 && DisplaySpriteList.TryGetValue(data.Code, out var sprite))
            {
                var jobName = GetJobNameForId(ctrl.OverrideClassId);
                var spr = $"Assets/Sprites/Weapons/{jobName}/{(ctrl.IsMale ? $"Male/{jobName}_M_" : $"Female/{jobName}_F_")}{sprite}.spr";
                if (DoesAddressableExist<RoSpriteData>(spr))
                    weaponSpriteFile = spr;
                else
                    Debug.Log($"Weapon sprite {data.Sprite} could not be loaded for {ctrl.Name} (Full path {spr})");
            }

            if (!PlayerWeaponLookup.TryGetValue(ctrl.OverrideClassId, out var weaponsByJob))
            {
                Destroy(oldObj);
                return;
            }

            if (!weaponsByJob.TryGetValue(weaponClass, out var weapon))
            {
                if(offHand == 0)
                    Debug.Log($"Could not load default weapon sprite for weapon class {ctrl.WeaponClass} for job {ctrl.OverrideClassId}");
                else
                    Debug.Log($"Could not load default weapon sprite for weapon class {ctrl.WeaponClass}/{offHand} for job {ctrl.OverrideClassId}");
                Destroy(oldObj);
                return;
            }

            if (string.IsNullOrWhiteSpace(weaponSpriteFile))
            {
                weaponSpriteFile = ctrl.IsMale ? weapon.SpriteMale : weapon.SpriteFemale;
                if (isEffect)
                    weaponSpriteFile = ctrl.IsMale ? weapon.EffectMale : weapon.EffectFemale;
            }

            if (string.IsNullOrWhiteSpace(weaponSpriteFile) && isEffect)
            {
                Destroy(oldObj);
                return;
            }

            var weaponObj = new GameObject("Weapon");
            weaponObj.layer = LayerMask.NameToLayer("Characters");
            weaponObj.transform.SetParent(ctrl.transform, false);
            weaponObj.transform.localPosition = Vector3.zero;

            var weaponSprite = weaponObj.AddComponent<RoSpriteAnimator>();

            weaponSprite.Parent = ctrl.SpriteAnimator;
            weaponSprite.SpriteOrder = 8;
            if (isEffect)
                weaponSprite.SpriteOrder = 5;

            if(!isEffect)
                ctrl.SpriteAnimator.PreferredAttackMotion = ctrl.IsMale ? weapon.AttackMale : weapon.AttackFemale;
            ctrl.SpriteAnimator.ChildrenSprites.Add(weaponSprite);

            ctrl.AttachedComponents[attachPosition] = weaponObj;

            AddressableUtility.LoadRoSpriteData(ctrl.gameObject, weaponSpriteFile, spriteData =>
            {
                Destroy(oldObj);
                weaponSprite.OnSpriteDataLoad(spriteData);
            });
            if (!isEffect) LoadAndAttachWeapon(ctrl, int.MaxValue); //attach effect sprite too
        }


        public void LoadAndAttachEquipmentSprite(ServerControllable ctrl, int itemId, EquipPosition position, int priority)
        {
            GameObject oldObj = null;
            if (ctrl.AttachedComponents.TryGetValue(position, out var existing))
            {
                ctrl.SpriteAnimator.ChildrenSprites.Remove(existing.GetComponent<RoSpriteAnimator>());
                ctrl.AttachedComponents.Remove(position);
                oldObj = existing;
            }

            if (!ItemIdLookup.TryGetValue(itemId, out var hat) || !DisplaySpriteList.TryGetValue(hat.Code, out var hatSprite))
            {
                Destroy(oldObj);
                return;
            }

            var headgearObj = new GameObject(position.ToString());
            headgearObj.layer = LayerMask.NameToLayer("Characters");
            headgearObj.transform.SetParent(ctrl.transform, false);
            headgearObj.transform.localPosition = new Vector3(0f, 0f, -0.05f);

            var headgearSprite = headgearObj.AddComponent<RoSpriteAnimator>();

            headgearSprite.Parent = ctrl.SpriteAnimator;
            headgearSprite.SpriteOrder = priority; //weapon is 5 so we should be below that
            ctrl.SpriteAnimator.ChildrenSprites.Add(headgearSprite);

            string spriteName;
            if (position == EquipPosition.Shield)
            {
                var jobName = GetJobNameForId(ctrl.OverrideClassId);
                spriteName = $"Assets/Sprites/Shields/{jobName}/{(ctrl.IsMale ? $"Male/{jobName}_M_" : $"Female/{jobName}_F_")}{hatSprite}.spr";
            }
            else
            {
                spriteName = $"Assets/Sprites/Headgear/{(ctrl.IsMale ? "Male/남_" : "Female/여_")}{hatSprite}.spr";
            }

            // var folderName = position != EquipPosition.Shield ? "Headgear" : $"Shields/{GetJobNameForId(ctrl.ClassId)}";


            ctrl.AttachedComponents[position] = headgearObj;

            if (!DoesAddressableExist<RoSpriteData>(spriteName))
            {
                Debug.LogWarning($"Could not load equipment sprite at path: {spriteName}");
                Destroy(oldObj);
            }
            else
                AddressableUtility.LoadRoSpriteData(ctrl.gameObject, spriteName, spriteData =>
                {
                    Destroy(oldObj);
                    headgearSprite.OnSpriteDataLoad(spriteData);
                });
        }

        public void AttachEmote(ServerControllable target, int emoteId)
        {
            // if (emoteId >= 34)
            //     emoteId--; //34 is the chat prohibited marker, but it isn't actually an emote within emotes.spr

            if (emoteId >= 200)
                emoteId -= 143; //these are dice, which sprites starts at 57. They use an out of range value so you can't send packet to get a specific roll.
            else if (EmoteDataTable.TryGetValue(emoteId, out var id))
                emoteId = id.Sprite;

            var go = new GameObject("Emote");
            //go.layer = LayerMask.NameToLayer("Characters");
            var billboard = go.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;

            var emote = go.AddComponent<EmoteController>();
            emote.AnimationId = emoteId;
            emote.Target = target.gameObject;

            var height = 50f;
            var spriteData = target.SpriteAnimator?.SpriteData;
            if (spriteData != null)
            {
                //seated players use their (lower) sitting height, falling back to standing if it wasn't baked
                var seated = target.CharacterType == CharacterType.Player
                    && target.SpriteAnimator.State == SpriteState.Sit
                    && spriteData.SittingHeight > 0;
                height = seated ? spriteData.SittingHeight : spriteData.StandingHeight;
            }
            if (target.CharacterType == CharacterType.Player)
                height += 12;
            if (height > 100)
                height = 120;

            var child = new GameObject("Sprite");
            //child.layer = LayerMask.NameToLayer("Characters");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = Vector3.zero + new Vector3(0, height / 30f, -0.01f);
            child.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var sr = child.AddComponent<RoSpriteRendererStandard>();
            sr.SecondPassForWater = false;
            sr.UpdateAngleWithCamera = false;

            var sprite = child.AddComponent<RoSpriteAnimator>();
            emote.RoSpriteAnimator = sprite;
            sprite.Type = SpriteType.Npc;
            sprite.State = SpriteState.Dead;
            //sprite.LockAngle = true;
            sprite.SpriteRenderer = sr;
            sprite.SpriteOrder = 50;

            AddressableUtility.LoadRoSpriteData(go, "Assets/Sprites/Misc/emotion.spr", emote.OnFinishLoad);
        }

        public void AttachPrefabToControllable(ServerControllable target, string prefabName)
        {
            var loader = Addressables.LoadAssetAsync<GameObject>(prefabName);
            loader.Completed += ah =>
            {
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    var obj2 = GameObject.Instantiate(ah.Result, target.transform, false);
                    obj2.transform.localPosition = Vector3.zero;

                    var sprite = obj2.GetComponent<RoSpriteAnimator>();
                    if (sprite != null)
                    {
                        sprite.Controllable = target;
                        target.SpriteAnimator = sprite;
                    }

                    target.EntityObject = obj2;

                    if (target.EntityObject.TryGetComponent<IEntityActionHandler>(out var handler))
                    {
                        handler.ChangeCharacterState(target.CharacterState);
                        target.PrefabActionHandler = handler;
                    }
                }
            };
        }
    }
}

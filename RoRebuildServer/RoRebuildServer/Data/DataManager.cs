using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.Data.ServerConfigScript;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;
using RoRebuildServer.ScriptSystem;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;
using RoRebuildServer.Simulation.Util;
using RoServerScript;

namespace RoRebuildServer.Data;

#pragma warning disable CS8618

public static class DataManager
{
    public static ReadOnlyDictionary<int, MonsterDatabaseInfo> MonsterIdLookup;
    public static ReadOnlyDictionary<string, MonsterDatabaseInfo> MonsterCodeLookup;
    public static ReadOnlyDictionary<string, MonsterDatabaseInfo> MonsterNameLookup;
    public static Dictionary<string, MonsterSkillAiBase> MonsterSkillAiHandlers;
    public static List<string> MvpMonsterCodes;
    public static List<InstanceEntry> InstanceList;
    public static ReadOnlyDictionary<string, Action<IServerMapConfig>> MapConfigs;

    public static Assembly ScriptAssembly;
    public static NpcBehaviorManager NpcManager;

    public static ReadOnlyDictionary<CharacterSkill, SkillData> SkillData;
    public static ReadOnlyDictionary<int, JobInfo> JobInfo;
    public static ReadOnlyDictionary<string, int> JobIdLookup;
    public static ReadOnlyDictionary<string, int> ItemIdByName;
    public static ReadOnlyDictionary<int, ItemInfo> ItemList;
    public static Dictionary<string, int> EffectIdForName;
    public static ReadOnlyDictionary<string, MonsterDropData> MonsterDropData;
    public static ReadOnlyDictionary<int, PlayerSkillTree> SkillTree; //SkillTree[Job][Skill] { (Prereq, lvl) } 
    public static ReadOnlyDictionary<int, int[]> JobMaxHpLookup;
    public static ReadOnlyDictionary<int, int[]> JobMaxSpLookup;
    public static ReadOnlyDictionary<string, SavePosition> SavePoints;
    public static ReadOnlyDictionary<int, int> JobExtendsList;
    
    public static ReadOnlyDictionary<string, int> WeaponClasses;
    public static ReadOnlyDictionary<string, HashSet<int>> EquipGroupInfo;
    public static ReadOnlyDictionary<int, WeaponInfo> WeaponInfo;
    public static ReadOnlyDictionary<int, ArmorInfo> ArmorInfo;
    public static ReadOnlyDictionary<int, CardInfo> CardInfo;
    public static ReadOnlyDictionary<int, AmmoInfo> AmmoInfo;
    public static ReadOnlyDictionary<int, UseItemInfo> UseItemInfo;
    public static ReadOnlyDictionary<string, List<string>> ItemMonsterSummonList;
    public static ReadOnlyDictionary<string, List<int>> ItemBoxSummonList;
    public static ReadOnlyDictionary<string, int> TagToIdLookup;
    public static HashSet<int> ValidEmotes;

    private static List<MonsterDatabaseInfo> monsterStats;
    private static List<List<MonsterAiEntry>> monsterAiList;
    private static ReadOnlyDictionary<string, MapEntry> mapLookup;
    private static ReadOnlyDictionary<string, MapFlags> mapFlags;
    private static List<MapEntry> mapList;
    private static int[] refineSuccessTable;
    public static int[] JobBonusTable;

    public static Dictionary<int, List<string>> CombosForEquipmentItem = new();
    public static Dictionary<string, int[]> ItemsInCombo = new();
    public static Dictionary<string, ItemInteractionBase> EquipmentComboInteractions = new();

    public static ServerConfigScriptManager ServerConfigScriptManager;

    public static List<MapEntry> Maps => mapList;
    
    public static ExpChart ExpChart;
    public static ElementChart ElementChart;
    public static int ServerVersionNumber;

    public static bool CanMemoMapForWarpPortalUse(string mapName) => mapFlags.TryGetValue(mapName, out var map) && map.HasFlag(MapFlags.CanMemo);
    public static MapFlags GetFlagsForMap(string mapName) => mapFlags.TryGetValue(mapName, out var flags) ? flags : MapFlags.None;
    public static bool IsJobInEquipGroup(string equipGroup, int job) => EquipGroupInfo.TryGetValue(equipGroup, out var set) && set.Contains(job);
    public static int GetEffectForItem(int item) => UseItemInfo.TryGetValue(item, out var effect) ? effect.Effect : -1;
    public static int GetWeightForItem(int item) => ItemList.TryGetValue(item, out var itemOut) ? itemOut.Weight : 0;
    public static ItemInfo? GetItemInfoById(int id) => ItemList.TryGetValue(id, out var item) ? item : null;
    public static int GetRefineSuccessForItem(int rank, int startingRefine) => refineSuccessTable[startingRefine * 5 + rank];
    public static Memory<int> GetJobBonusesForLevel(int job, int level) => JobBonusTable.AsMemory(job * 70 * 6 + (level - 1) * 6, 6);

    public static List<MonsterAiEntry> GetAiStateMachine(MonsterAiType monsterType)
    {
        return monsterAiList[(int)monsterType];
    }

    public static int GetSpForSkill(CharacterSkill skill, int level)
    {
        if (!DataManager.SkillData.TryGetValue(skill, out var data))
            return 0;

        if (data.SpCost == null || data.SpCost.Length == 0)
            return 0;

        if (data.SpCost.Length < level)
            level = data.SpCost.Length;

        return data.SpCost[level-1];
    }
    
    public static void RegisterItem(string name, ItemInteractionBase item)
    {
        if (!ItemIdByName.TryGetValue(name, out var id))
        {
            ServerLogger.LogWarning($"Could not attach item interaction to item as the item list does not contain: {name}");
            return;
        }

        ItemList[id].Interaction = item;
    }

    public static void RegisterComboItem(string name, ItemInteractionBase itemInteraction, List<string> comboItems)
    {
        Span<int> itemIds = stackalloc int[comboItems.Count];

        if (EquipmentComboInteractions.ContainsKey(name))
        {
            ServerLogger.LogWarning($"Could not create ComboItem {name} as a combo of the same name already exists.");
            return;
        }

        for (var i = 0; i < comboItems.Count; i++)
        {
            var item = comboItems[i];
            if (!ItemIdByName.TryGetValue(item, out var id))
            {
                ServerLogger.LogWarning($"Could not create ComboItem {name} as the item {item} in the set could not be found.");
                return;
            }
            itemIds[i] = id;
        }

        var hashSet = new int[comboItems.Count];
        ItemsInCombo.Add(name, hashSet);
        EquipmentComboInteractions.Add(name, itemInteraction);

        for (var i = 0; i < itemIds.Length; i++)
        {
            var item = itemIds[i];
            if (!CombosForEquipmentItem.TryGetValue(item, out var comboSets))
            {
                comboSets = new List<string>();
                CombosForEquipmentItem.Add(item, comboSets);
            }

            comboSets.Add(name);
            hashSet[i] = item;
        }
    }

    public static void RegisterNpc(string name, string map, string? signalName, string sprite, int x, int y, int facing, int w, int h, bool hasInteract, bool hasTouch, NpcBehaviorBase npcBehavior)
    {
        if (!MonsterCodeLookup.TryGetValue(sprite, out var md))
        {
            ServerLogger.LogError($"Could not load NPC '{name}' as the sprite {sprite} was not recognized by the server.");
            return;
        }

        NpcManager.RegisterNpc(name, map, signalName, md.Id, x, y, (Direction)facing, w, h, hasInteract, hasTouch, npcBehavior);
    }

    public static void RegisterEvent(string name, NpcBehaviorBase npcBehavior)
    {
        NpcManager.RegisterEvent(name, npcBehavior);
    }

    public static void RegisterMonsterSkillHandler(string name, MonsterSkillAiBase handler, bool isUnassignedAiType = false)
    {
        MonsterSkillAiHandlers.Add(name, handler);
        if (isUnassignedAiType)
            handler.IsUnassignedAiType = true;
    }

    public static void ReloadScripts(bool loadExistingAssembly = false)
    {
        var loader = new DataLoader();
        
        Time.ResetDiagnosticsTimer();
        
        ServerVersionNumber = loader.LoadVersionInfo();
        monsterStats = loader.LoadMonsterStats();
        monsterAiList = loader.LoadAiStateMachines();
        ExpChart = loader.LoadExpChart();
        EffectIdForName = loader.LoadEffectIds();
        JobInfo = loader.LoadJobs();
        JobIdLookup = loader.GetJobIdLookup(JobInfo);
        MvpMonsterCodes = loader.LoadMvpList();
        SkillData = loader.LoadSkillData();
        JobMaxHpLookup = loader.LoadMaxHpChart();
        JobMaxSpLookup = loader.LoadMaxSpChart();

        WeaponClasses = loader.LoadWeaponClasses();
        EquipGroupInfo = loader.LoadEquipGroups();
        var items = loader.LoadItemsRegular();
        ArmorInfo = loader.LoadItemsArmor(items);
        WeaponInfo = loader.LoadItemsWeapon(items);
        CardInfo = loader.LoadItemsCards(items);
        UseItemInfo = loader.LoadUseableItems(items); //dependent on effectId loaded earlier
        AmmoInfo = loader.LoadItemsAmmo(items);
        refineSuccessTable = loader.LoadRefineSuccessTable();

        ItemList = items.AsReadOnly();

        ItemIdByName = loader.GenerateItemIdByNameLookup();
        SavePoints = loader.LoadSavePoints().AsReadOnly();
        ElementChart = loader.LoadElementChart();
        MvpMonsterCodes = loader.LoadMvpList();

        var dataLoadTime = Time.SampleDiagnosticsTime();
        
        //load our compiled script assemblies
        if(!loadExistingAssembly)
            ScriptAssembly = ScriptLoader.LoadAssembly();
        else
            ScriptAssembly = ScriptLoader.LoadExisting();

        var assemblyBuildTime = Time.SampleDiagnosticsTime();

        NpcManager = new NpcBehaviorManager();
        if(ServerConfigScriptManager != null!)
            ServerConfigScriptManager.OnScriptReload();
        ServerConfigScriptManager = new ServerConfigScriptManager(ScriptAssembly);
        ServerConfigScriptManager.RegisterServerConfigAssembly(Assembly.GetExecutingAssembly());
        
        var monsterIdLookup = new Dictionary<int, MonsterDatabaseInfo>(monsterStats.Count);
        var monsterCodeLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);
        var monsterNameLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);

        foreach (var m in monsterStats)
        {
            monsterIdLookup.Add(m.Id, m);
            monsterCodeLookup.Add(m.Code, m);
            monsterNameLookup.TryAdd(m.Name, m);
        }

        MonsterIdLookup = monsterIdLookup.AsReadOnly();
        MonsterCodeLookup = monsterCodeLookup.AsReadOnly();
        MonsterNameLookup = monsterNameLookup.AsReadOnly();
        
        //things that require our compiled scripts

        loader.LoadNpcScripts(Assembly.GetAssembly(typeof(FirewallObjectEvent))!); //load from local assembly
        loader.LoadNpcScripts(ScriptAssembly);
        loader.LoadItemInteractions(ScriptAssembly);
        loader.LoadMonsterSkillAi(ScriptAssembly);
        MapConfigs = loader.LoadMapConfigs(ScriptAssembly);

        var assemblyLoadTime = Time.SampleDiagnosticsTime();

        //things that require other things loaded first
        loader.LoadMonsterSpawnMinions();
        var t1 = Time.SampleDiagnosticsSubTime();
        SkillTree = loader.LoadSkillTree();
        var t2 = Time.SampleDiagnosticsSubTime();
        MonsterDropData = loader.LoadMonsterDropChanceData(ServerConfigScriptManager);
        var t3 = Time.SampleDiagnosticsSubTime();
        ItemMonsterSummonList = loader.LoadMonsterSummonItemList();
        ItemBoxSummonList = loader.LoadItemBoxSummonList();
        var t4 = Time.SampleDiagnosticsSubTime();
        JobBonusTable = loader.LoadJobBonusTable();

        var t5 = Time.SampleDiagnosticsSubTime();
        ValidEmotes = loader.LoadEmotes();

        var t6 = Time.SampleDiagnosticsSubTime();
        ServerConfigScriptManager.UpdateItemValue(ItemList);

        var postTime = Time.SampleDiagnosticsTime();

        ServerLogger.Log($"DataManager loaded data in {Time.CurrentDiagnosticsTime():F2}s " +
                         $"(Data {dataLoadTime:F2}s, " +
                         $"Compile {assemblyBuildTime:F2}s, " +
                         $"Assembly Load {assemblyLoadTime:F2}s, " +
                         $"Post {postTime:F2}s)");
    }

    public static void Initialize(bool loadExisting = false)
    {
        var loader = new DataLoader();

        (mapLookup, mapFlags) = loader.LoadMaps();
        mapList = mapLookup.Values.ToList();
        InstanceList = loader.LoadInstances();

        ReloadScripts(loadExisting);
        ServerLogger.Log($"Server will identify as using protocol v{ServerVersionNumber}.");

        //special handling for if we start the map in single map only mode, removes all other maps from the server instance list
        var debug = ServerConfig.DebugConfig;
        if (debug.DebugMapOnly)
        {
            var okInstance = InstanceList.FirstOrDefault(i => i.Maps.Contains(debug.DebugMapName));
            if (okInstance == null)
            {
                ServerLogger.LogWarning($"Server started in Debug Map Only mode, but the specified map {debug.DebugMapName} was not found on the instance list.");
                return;
            }

            ServerLogger.Log($"Starting server in Debug Map Only mode. The map {debug.DebugMapName} is the only map available.");

            okInstance.Maps.RemoveAll(m => m != debug.DebugMapName);
            InstanceList.Clear();
            InstanceList.Add(okInstance);
        }
    }
}
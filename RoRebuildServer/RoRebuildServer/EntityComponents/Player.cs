﻿using System.Diagnostics;
using System.Numerics;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildZoneServer.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.ScriptSystem;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Items;
using RoRebuildServer.Simulation.Parties;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Player)]
public class Player : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;
    public CombatEntity CombatEntity = null!;

    public NetworkConnection Connection = null!;

    public Guid Id { get; set; }
    public int StorageId { get; set; }
    public int CharacterSlot { get; set; }
    public string Name { get; set; } = "Uninitialized Player";
    public HeadFacing HeadFacing;
    //public PlayerData Data { get; set; }
    [ScriptUseable] public bool IsAdmin { get; set; }
    public bool IsInNpcInteraction { get; set; }
    public bool IsMale => GetData(PlayerStat.Gender) == 0;
    public override string ToString() => $"Player:{Name}";

    [EntityIgnoreNullCheck] public NpcInteractionState NpcInteractionState = new();
    [EntityIgnoreNullCheck] public int[] CharData = new int[(int)PlayerStat.PlayerStatsMax];

    [EntityIgnoreNullCheck]
    public SavePosition SavePosition
    {
        get;
        set;
    } = new();
    [EntityIgnoreNullCheck] public MapMemoLocation[] MemoLocations = new MapMemoLocation[4];
    [EntityIgnoreNullCheck] public List<SkillCastInfo> IndirectCastQueue { get; set; } = null!;
    [EntityIgnoreNullCheck] public Dictionary<int, int>? AttackVersusTag { get; set; }
    [EntityIgnoreNullCheck] public Dictionary<int, int>? ResistVersusTag { get; set; }
    [EntityIgnoreNullCheck] public Dictionary<CharacterSkill, double> SkillSpecificCooldowns = new();
    public Dictionary<CharacterSkill, int> LearnedSkills = null!;
    public Dictionary<CharacterSkill, int>? GrantedSkills;

    public Dictionary<string, int>? NpcFlags;
    public ItemEquipState Equipment = null!;
    public CharacterBag? Inventory;
    public CharacterBag? CartInventory;
    public CharacterBag? StorageInventory;
    public VendingState? VendingState;
    public EntityValueList<float> RecentAttackersList = null!;
    private float lastAttackerListCheckUpdate;
    public float ShoutCooldown;
    private Memory<int>? jobStatBonuses;

    public SpecialPlayerActionState SpecialState;
    public Position SpecialStateTarget;
    public PlayerSkillTree? JobSkillTree;

    public Party? Party;
    public int PartyMemberId;
    public bool HasEnteredServer;

    public PlayerFollower PlayerFollower;
    public bool HasCart => (PlayerFollower & PlayerFollower.AnyCart) > 0;
    public bool HasBird => (PlayerFollower & PlayerFollower.Falcon) > 0;

    public StatusTriggerFlags OnMeleeAttackStatusFlags;
    public StatusTriggerFlags OnRangedAttackStatusFlags;
    public StatusTriggerFlags OnMeleeAttackStatusSelfFlags;
    public StatusTriggerFlags WhenAttackedStatusFlags;
    public AttackEffectTriggers OnAttackTriggerFlags;

    [EntityIgnoreNullCheck] public static Action<Player>? OnLevelUpEvent;

    public int GetItemIdForEquipSlot(EquipSlot slot) => Equipment.ItemIds[(int)slot];

    public bool DoesCharacterKnowSkill(CharacterSkill skill, int level)
    {
        if (LearnedSkills.TryGetValue(skill, out var learned) && learned >= level)
            return true;
        if (GrantedSkills != null && GrantedSkills.TryGetValue(skill, out var granted) && granted >= level)
            return true;
        return false;
    }
    public int MaxLearnedLevelOfSkill(CharacterSkill skill) => LearnedSkills.TryGetValue(skill, out var learned) ? learned : 0;

    public int MaxAvailableLevelOfSkill(CharacterSkill skill)
    {
        return int.Max(
            LearnedSkills.TryGetValue(skill, out var learned) ? learned : 0,
            GrantedSkills != null && GrantedSkills.TryGetValue(skill, out var granted) ? granted : 0
        );

    }

    [ScriptUseable] public int GetNpcFlag(string flag) => NpcFlags != null && NpcFlags.TryGetValue(flag, out var val) ? val : 0;

    [ScriptUseable]
    public void SetNpcFlag(string flag, int val)
    {
        NpcFlags ??= new Dictionary<string, int>();
        NpcFlags[flag] = val;
    }

    public Entity Target { get; set; }

    public bool AutoAttackLock
    {
        get;
        set;
    }
    private float regenHpTickTime { get; set; }
    private float regenSpTickTime { get; set; }
    private bool isSittingHpTick;
    private bool isSittingSpTick;

    private const float HpRegenTickTime = 6f;
    private const float SpRegenTickTime = 6f;
    public void ResetRegenTickTime()
    {
        regenHpTickTime = HpRegenTickTime / 2f;
        regenSpTickTime = HpRegenTickTime / 2f;
    }

    public int WeaponClass => Equipment.MainHandWeapon.WeaponClass;

#if DEBUG
    private float actionCooldown;
    public float InputActionCooldown
    {
        get => actionCooldown;
        set
        {
            actionCooldown = value;
            if (actionCooldown > 5f)
                ServerLogger.LogWarning($"Warning! Attempting to set player cooldown to time exceeding 5s! Stack Trace:\n" + Environment.StackTrace);
        }
    }
#else
    public float InputActionCooldown;
#endif

    public float LastEmoteTime; //we'll probably need to have like, a bunch of timers at some point...
    public double SkillCooldownTime;

    //helper functions for item effect handlers
    [ScriptUseable] public int CharacterLevel => GetData(PlayerStat.Level);
    [ScriptUseable] public int JobLevel => GetData(PlayerStat.JobLevel);
    [ScriptUseable] public int JobId => GetData(PlayerStat.Job);

    //stats that can't apply to monsters
    //[EntityIgnoreNullCheck] public readonly int[] PlayerStatData = new int[(int)(CharacterStat.CharacterStatsMax - CharacterStat.MonsterStatsMax)];
    [EntityIgnoreNullCheck] public Dictionary<CharacterStat, int> PlayerStatData = new();

    [ScriptUseable] public int GetData(PlayerStat type) => CharData[(int)type];
    [ScriptUseable] public void SetData(PlayerStat type, int val) => CharData[(int)type] = val;
    [ScriptUseable] public int GetStat(CharacterStat type) => CombatEntity.GetStat(type);
    [ScriptUseable] public int GetEffectiveStat(CharacterStat type) => CombatEntity.GetEffectiveStat(type);
    [ScriptUseable] public float GetTiming(TimingStat type) => CombatEntity.GetTiming(type);
    [ScriptUseable] public void SetStat(CharacterStat type, int val) => CombatEntity.SetStat(type, val);
    [ScriptUseable] public void SetStat(CharacterStat type, float val) => CombatEntity.SetStat(type, (int)val);
    [ScriptUseable] public void AddStat(CharacterStat type, int val) => CombatEntity.AddStat(type, val);
    [ScriptUseable] public void SubStat(CharacterStat type, int val) => CombatEntity.SubStat(type, val);
    [ScriptUseable] public void SetTiming(TimingStat type, float val) => CombatEntity.SetTiming(type, val);
    [ScriptUseable] public int GetZeny() => CharData[(int)PlayerStat.Zeny];

    [ScriptUseable]
    public void AddZeny(int val)
    {
        var v = CharData[(int)PlayerStat.Zeny];
        if (int.MaxValue - val < v) //probably safe?
            CharData[(int)PlayerStat.Zeny] = int.MaxValue;
        else
            CharData[(int)PlayerStat.Zeny] += val;
    }

    [ScriptUseable]
    public void DropZeny(int val)
    {
        var v = CharData[(int)PlayerStat.Zeny] - val;
        CharData[(int)PlayerStat.Zeny] = v > 0 ? v : 0;
        CommandBuilder.SendUpdateZeny(this);
    }

    //how much it costs (cumulatively) to have a stat at a specific level. Should be in its own file...
    private static readonly int[] cumulativeStatPointCost = new[]
    {
          2,   4,   6,   8,  10,  12,  14,  16,  18,  20,  22,  25,  28,  31,  34,  37,  40,  43,  46,  49,
         52,  56,  60,  64,  68,  72,  76,  80,  84,  88,  92,  97, 102, 107, 112, 117, 122, 127, 132, 137,
        142, 148, 154, 160, 166, 172, 178, 184, 190, 196, 202, 209, 216, 223, 230, 237, 244, 251, 258, 265,
        272, 280, 288, 296, 304, 312, 320, 328, 336, 344, 352, 361, 370, 379, 388, 397, 406, 415, 424, 433,
        442, 452, 462, 472, 482, 492, 502, 512, 522, 532, 542, 553, 564, 575, 586, 597, 608, 619, 630
    };

    //how many stat points you have for your base level. Should be in its own file too...
    private static readonly int[] statPointsEarnedByLevel = new[]
    {
         66,  69,  72,  75,  78,   82,   86,   90,   94,   98,  103,  108,  113,  118,  123,  129,  135,  141,  147,  153,
        160, 167, 174, 181, 188,  196,  204,  212,  220,  228,  237,  246,  255,  264,  273,  283,  293,  303,  313,  323,
        334, 345, 356, 367, 378,  390,  402,  414,  426,  438,  451,  464,  477,  490,  503,  517,  531,  545,  559,  573,
        588, 603, 618, 633, 648,  664,  680,  696,  712,  728,  745,  762,  779,  796,  813,  831,  849,  867,  885,  903,
        922, 941, 960, 979, 998, 1018, 1038, 1058, 1078, 1098, 1119, 1140, 1161, 1182, 1203, 1225, 1247, 1269, 1291,
    };

    public void Reset()
    {
        Entity = Entity.Null;
        Target = Entity.Null;
        Character = null!;
        CombatEntity = null!;
        Connection = null!;
        InputActionCooldown = 0f;
        HeadFacing = HeadFacing.Center;
        AutoAttackLock = false;
        Id = Guid.Empty;
        Name = "Uninitialized Player";
        //Data = new PlayerData(); //fix this...
        regenHpTickTime = 0;
        regenSpTickTime = 0;
        NpcInteractionState.Reset();
        IsAdmin = false;
        Array.Clear(CharData);
        PlayerStatData.Clear();
        LastEmoteTime = 0;
        StorageId = -1;
        HasEnteredServer = false;
        LearnedSkills = null!;
        GrantedSkills = null;
        NpcFlags = null!;
        jobStatBonuses = null;
        SpecialState = SpecialPlayerActionState.None;
        SpecialStateTarget = Position.Invalid;
        PlayerFollower = PlayerFollower.None;
        JobSkillTree = null;

        if (AttackVersusTag != null)
            AttackVersusTag.Clear();

        if (ResistVersusTag != null)
            ResistVersusTag.Clear();

        if (Inventory != null)
            CharacterBag.Return(Inventory);

        if (CartInventory != null)
            CharacterBag.Return(CartInventory);

        if (StorageInventory != null)
            CharacterBag.Return(StorageInventory);

        IndirectCastQueue.Clear();
        SkillSpecificCooldowns.Clear();

        Array.Clear(MemoLocations);

        Inventory = null;
        CartInventory = null;
        StorageInventory = null;
        Equipment = null!;
        EntityValueListPool<float>.Return(RecentAttackersList);
        RecentAttackersList = null!;
        VendingState = null;

        isSittingHpTick = false;
        isSittingSpTick = false;

        if (Party != null)
        {
            Party.UpdateOfflineMembers(); //our entity should be expired so it should get removed
            Party = null;
        }

        PartyMemberId = 0;

        SavePosition.Reset();
    }

    public void Init()
    {
        LearnedSkills ??= new Dictionary<CharacterSkill, int>();

        foreach (var skill in LearnedSkills)
            SkillHandler.ApplyPassiveEffects(skill.Key, CombatEntity, skill.Value);

        if (Equipment == null!)
            Equipment = new ItemEquipState();

        Equipment.Player = this;
        StorageId = -1;
        Equipment.RunAllOnEquip();

        if (IndirectCastQueue == null!)
            IndirectCastQueue = new List<SkillCastInfo>(4);

        SetStat(CharacterStat.Level, GetData(PlayerStat.Level));
        Character.DisplayType = CharacterDisplayType.Player;
        CombatEntity.Faction = 1;

        IsAdmin = ServerConfig.DebugConfig.UseDebugMode;
        RecentAttackersList = EntityValueListPool<float>.Get();
        ResetRegenTickTime();

        if (CharacterLevel == 0)
            SetData(PlayerStat.Level, 1); //why
        if (JobLevel == 0)
            SetData(PlayerStat.JobLevel, 1);
        var hasStatFix = false;
        for (var i = 0; i < 6; i++)
            if (GetData(PlayerStat.Str + i) <= 0)
            {
                SetData(PlayerStat.Str + i, 1); //should never happen, but lookups will break if they have 0 in any stat so fix it
                hasStatFix = true;
            }
        if (hasStatFix)
            ServerLogger.LogError($"Player {this} initialized with one or more stats set to 0, something must have gone wrong loading their data.");

        //if this is their first time logging in, they get a free Knife
        var isNewCharacter = GetData(PlayerStat.Status) == 0 || (Inventory == null && GetData(PlayerStat.Level) <= 3);
        if (GetData(PlayerStat.Level) <= 1 && GetData(PlayerStat.Job) == 0
            && Equipment.GetEquipmentIdBySlot(EquipSlot.Weapon) <= 0 && Equipment.GetEquipmentIdBySlot(EquipSlot.Body) <= 0)
            isNewCharacter = true;
        if (isNewCharacter)
        {
            var hasEmptyInventory = Inventory == null || Inventory.BagWeight <= 0;
            if (DataManager.ItemIdByName.TryGetValue("Knife", out var knife))
            {
                var item = new ItemReference(knife, 1);
                var bagId = AddItemToInventory(item);
                Equipment.EquipItem(bagId, EquipSlot.Weapon);
            }
            if (DataManager.ItemIdByName.TryGetValue("Cotton_Shirt", out var shirt))
            {
                var item = new ItemReference(shirt, 1);
                var bagId = AddItemToInventory(item);
                Equipment.EquipItem(bagId, EquipSlot.Body);
            }

            if (hasEmptyInventory && DataManager.ItemIdByName.TryGetValue("Apple", out var apple))
            {
                var item = new ItemReference(apple, 15);
                AddItemToInventory(item);
            }
            SetData(PlayerStat.Status, 1);
            UpdateStats(false, false); //update without sending update because we want to trigger inventory update too
            CombatEntity.FullRecovery(true, true);
            CommandBuilder.SendUpdatePlayerData(this, false, false, false);
        }
        else
        {
            EnsureNoviceSkillPointsCorrectlyAssigned(); //if they're not a novice we can give them all the novice skills since they should have them
            UpdateStats();
        }

        //IsAdmin = true; //for now
    }

    public void Revive(int hpPercent)
    {
        if (Character.State != CharacterState.Dead)
            return;

        var maxHp = GetStat(CharacterStat.MaxHp);

        if (GetStat(CharacterStat.FullRevive) > 0)
        {
            SetStat(CharacterStat.Hp, maxHp);
            SetStat(CharacterStat.Sp, GetStat(CharacterStat.MaxSp));
        }
        else
        {
            var resHp = maxHp * hpPercent / 100;
            resHp = int.Clamp(resHp, 1, maxHp);

            SetStat(CharacterStat.Hp, resHp);
        }

        Character.ResetState(true);
        Character.SetSpawnImmunity();

        //if (VendingState != null && Character.Events != null)
        //{
        //    if (!VendingState.VendProxy.TryGet<Npc>(out var npc) || npc.DisplayType != NpcDisplayType.VendingProxy)
        //        return;

        //    npc.OnInteract(this);

        //}
    }

    public void Respawn()
    {
        if (VendingState != null)
        {

        }
        ReturnToSavePoint();
    }

    public bool IsSkillOnCooldown(CharacterSkill skill)
    {
        if (SkillSpecificCooldowns.TryGetValue(skill, out var cooldown))
        {
            if (cooldown > Time.ElapsedTime)
                return true;
            SkillSpecificCooldowns.Remove(skill);
        }

        return false;
    }

    public void WriteCharacterStorageToDatabase()
    {
        if (StorageInventory == null)
            return;

        var req = new StorageSaveRequest(this);
        RoDatabase.EnqueueDbRequest(req);
    }

    public void WriteCharacterToDatabase()
    {
        SaveCharacterToData();
        var req = new SaveCharacterRequest(this);
        RoDatabase.EnqueueDbRequest(req);
    }

    public int AddItemToInventory(ItemReference item)
    {
        Inventory ??= CharacterBag.Borrow();
        return Inventory.AddItem(item);
    }

    public bool CanPickUpItem(ItemReference item)
    {
        if (Inventory == null)
            return true;
        if (Inventory.UsedSlots >= CharacterBag.MaxBagSlots)
            return false;

        if (item.Weight * item.Count + Inventory.BagWeight > GetStat(CharacterStat.WeightCapacity))
            return false;

        if (item.Type == ItemType.RegularItem && Inventory.RegularItems.TryGetValue(item.Item.Id, out var existing))
            return item.Count + existing.Count < 30000;

        //unique items will end up as a separate entry no matter the id so no need to see if stack size exceeds limits

        return true;
    }

    public void SendPlayerUpdateData(OutboundMessage packet, bool sendInventory, bool sendCart, bool refreshSkills)
    {
        foreach (var dataType in PlayerClientStatusDef.PlayerUpdateData)
            packet.Write(GetData(dataType));

        var (atk1, atk2) = CombatEntity.CalculateAttackPowerRange(false);
        var (matk1, matk2) = CombatEntity.CalculateAttackPowerRange(true);

        foreach (var statType in PlayerClientStatusDef.PlayerUpdateStats)
        {
            switch (statType)
            {
                case CharacterStat.Attack:
                    packet.Write(atk1 + Equipment.MainHandWeapon.MinRefineAtkBonus);
                    break;
                case CharacterStat.Attack2:
                    packet.Write(atk2 + Equipment.MainHandWeapon.MaxRefineAtkBonus);
                    break;
                case CharacterStat.MagicAtkMin:
                    packet.Write(matk1 + Equipment.MainHandWeapon.MinRefineAtkBonus);
                    break;
                case CharacterStat.MagicAtkMax:
                    packet.Write(matk2 + Equipment.MainHandWeapon.MaxRefineAtkBonus);
                    break;
                case CharacterStat.Def:
                    packet.Write(CombatEntity.GetEffectiveStat(CharacterStat.Def));
                    break;
                case CharacterStat.MDef:
                    packet.Write(CombatEntity.GetEffectiveStat(CharacterStat.MDef));
                    break;
                case CharacterStat.PerfectDodge:
                    packet.Write(CombatEntity.GetEffectiveStat(CharacterStat.PerfectDodge));
                    break;
                case CharacterStat.AddLuk:
                    if (CombatEntity.HasBodyState(BodyStateFlags.Curse))
                        packet.Write(-CombatEntity.GetStat(CharacterStat.Luk));
                    else
                        packet.Write(CombatEntity.GetStat(CharacterStat.AddLuk));
                    break;
                default:
                    packet.Write(GetStat(statType));
                    break;
            }
        }

        packet.Write(GetTiming(TimingStat.AttackDelayTime));
        packet.Write(Inventory?.BagWeight ?? 0);
        packet.Write(CartInventory?.BagWeight ?? 0);
        packet.Write(refreshSkills);

        if (refreshSkills)
        {
            packet.Write((short)LearnedSkills.Count);
            foreach (var skill in LearnedSkills)
            {
                packet.Write((short)skill.Key);
                packet.Write((byte)skill.Value);
            }

            if (GrantedSkills == null)
                packet.Write((short)0);
            else
            {
                packet.Write((short)GrantedSkills.Count);
                foreach (var skill in GrantedSkills)
                {
                    packet.Write((short)skill.Key);
                    packet.Write((byte)skill.Value);
                }
            }
        }
        packet.Write(sendInventory);
        if (sendInventory)
        {
            Inventory.TryWrite(packet, true);
            packet.Write((byte)(sendCart ? 1 : 0));
            if (sendCart)
                CartInventory.TryWrite(packet, true);
            //StorageInventory.TryWrite(packet, true);
            for (var i = 0; i < 10; i++)
                packet.Write(Equipment.ItemSlots[i]);
            packet.Write(Equipment.AmmoId);
        }
    }

    public bool TryRemoveItemFromInventory(int type, int count, bool sendPacket = false)
    {
        Debug.Assert(count < short.MaxValue);

        if (Inventory != null && Inventory.RemoveItem(new RegularItem() { Id = type, Count = (short)count }))
        {
            if (sendPacket)
                CommandBuilder.RemoveItemFromInventory(this, type, count);

            return true;
        }

        return false;
    }

    public int GainBaseExpFromMonster(int exp, MonsterDatabaseInfo monsterBase)
    {
        var scale = GetStat(CharacterStat.AddExpRaceFormless + (int)monsterBase.Race);
        if (scale != 0)
            exp = exp * (100 + scale) / 100;

        return GainBaseExp(exp);
    }

    public int GainBaseExp(int exp)
    {
        var level = GetData(PlayerStat.Level);
        if (CharacterLevel >= 99 || exp == 0)
            return 0;

        var curExp = GetData(PlayerStat.Experience);
        var requiredExp = DataManager.ExpChart.ExpRequired[level];

        if (exp > requiredExp)
            exp = requiredExp; //cap to 1 level per kill

        curExp += exp;

        if (curExp < requiredExp)
        {
            SetData(PlayerStat.Experience, curExp);
            return exp;
        }

        while (curExp >= requiredExp && level < 99)
        {
            curExp -= requiredExp;

            LevelUp();
            level++;

            if (level < 99)
                requiredExp = DataManager.ExpChart.ExpRequired[level];
        }

        SetData(PlayerStat.Experience, curExp);

        if (Party != null)
            CommandBuilder.NotifyPartyOfChange(Party, PartyMemberId, PartyUpdateType.UpdatePlayer);

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.LevelUp(Character, level, curExp);
        CommandBuilder.SendHealMulti(Character, 0, HealType.None);
        CommandBuilder.ChangeSpValue(this, GetStat(CharacterStat.Sp), GetStat(CharacterStat.MaxSp));
        CommandBuilder.ClearRecipients();

        return exp;
    }

    public int GainJobExpFromMonster(int exp, MonsterDatabaseInfo monsterBase)
    {
        var scale = GetStat(CharacterStat.AddExpRaceFormless + (int)monsterBase.Race);
        if (scale != 0)
            exp = exp * (100 + scale) / 100;

        return GainJobExp(exp);
    }

    public int GainJobExp(int exp)
    {
        var level = GetData(PlayerStat.JobLevel);
        var job = GetData(PlayerStat.Job);
        var levelCap = job == 0 ? 10 : 50;
        if (DataManager.JobInfo.TryGetValue(job, out var jobInfo))
            levelCap = jobInfo.MaxJobLevel;
        if (level >= levelCap)
            return 0;

        var curExp = GetData(PlayerStat.JobExperience);
        var requiredExp = DataManager.ExpChart.RequiredJobExp(job, level);
        if (requiredExp < 0)
            return 0;

        if (exp > requiredExp)
            exp = requiredExp; //cap to 1 level per kill

        curExp += exp;

        if (curExp < requiredExp)
        {
            SetData(PlayerStat.JobExperience, curExp);
            return exp;
        }

        var origLevel = level;

        while (curExp >= requiredExp && level < levelCap)
        {
            curExp -= requiredExp;

            level++;

            if (level < levelCap)
                requiredExp = DataManager.ExpChart.RequiredJobExp(job, level);
        }

        SetData(PlayerStat.JobLevel, level);
        SetData(PlayerStat.JobExperience, curExp);

        if (job == 0 && level == 10 && origLevel < level)
            CommandBuilder.SendServerEvent(this, ServerEvent.EligibleForJobChange, job);

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendEffectOnCharacterMulti(Character, DataManager.EffectIdForName["JobUp"]);
        CommandBuilder.ClearRecipients();

        UpdateStats(false);

        return exp;
    }

    public void AddStatPoints(Span<int> statChanges)
    {
        var level = GetData(PlayerStat.Level);
        var statPointsEarned = statPointsEarnedByLevel[level - 1];
        var statPointsUsed = 0;
        Span<int> newStats = stackalloc int[6];

        for (var i = 0; i < 6; i++)
        {
            if (statChanges[i] < 0)
            {
                ServerLogger.LogWarning($"Player {Name} is attempting to apply a negative number of stat points.");
                return;
            }

            var newValue = GetData(PlayerStat.Str + i) + statChanges[i];
            if (newValue > 99)
            {
                ServerLogger.LogWarning($"Player {Name} is attempting to raise one of their stat values above 99.");
                return;
            }

            statPointsUsed += cumulativeStatPointCost[newValue - 1];
            newStats[i] = newValue;
        }

        if (statPointsUsed > statPointsEarned)
        {
            ServerLogger.LogWarning($"Player {Name} is attempting to apply more stat points than they have.");
            return;
        }

        for (var i = 0; i < 6; i++)
            SetData(PlayerStat.Str + i, newStats[i]);

        UpdateStats(false);
    }

    public void StatPointReset()
    {
        for (var i = PlayerStat.Str; i <= PlayerStat.Luk; i++)
            SetData(i, 1);

        UpdateStats(false);
    }

    public void SkillReset()
    {
        var basic = LearnedSkills.TryGetValue(CharacterSkill.BasicMastery, out var level) ? level : 0;
        var firstAid = LearnedSkills.TryGetValue(CharacterSkill.FirstAid, out var aidLevel) ? aidLevel : 0;

        foreach (var skill in LearnedSkills)
            SkillHandler.RemovePassiveEffects(skill.Key, CombatEntity, skill.Value);

        LearnedSkills.Clear();

        if (basic > 0) LearnedSkills.Add(CharacterSkill.BasicMastery, basic);
        if (firstAid > 0) LearnedSkills.Add(CharacterSkill.FirstAid, firstAid);

        UpdateStats();
    }

    //if they're not a novice we can give them all the novice skills since they should have them
    private void EnsureNoviceSkillPointsCorrectlyAssigned()
    {
        if (JobId == 0)
            return;

        if (MaxLearnedLevelOfSkill(CharacterSkill.BasicMastery) < 8)
            AddSkillToCharacter(CharacterSkill.BasicMastery, 8);

        if (MaxLearnedLevelOfSkill(CharacterSkill.FirstAid) < 1)
            AddSkillToCharacter(CharacterSkill.FirstAid, 1);
    }

    public void UpdateStats(bool updateSkillData = true, bool sendUpdate = true)
    {
        var level = GetData(PlayerStat.Level);
        var job = GetData(PlayerStat.Job);
        var jobInfo = DataManager.JobInfo[job];

        if (!DataManager.SkillTree.TryGetValue(job, out JobSkillTree))
            JobSkillTree = null;

        if (level > 99 || level < 1)
        {
            ServerLogger.LogWarning($"Woah! The player '{Name}' has a level of {level}, that's not normal. We'll lower the level down to the cap.");
            level = Math.Clamp(level, 1, 99);
            SetData(PlayerStat.Level, level);
        }

        Character.ClassId = job;

        if (WeaponClass == 12) //bow
            SetStat(CharacterStat.Range, int.Max(1, Equipment.WeaponRange + MaxLearnedLevelOfSkill(CharacterSkill.VultureEye)));
        else
            SetStat(CharacterStat.Range, int.Max(1, Equipment.WeaponRange));

        SetStat(CharacterStat.Str, GetData(PlayerStat.Str));
        SetStat(CharacterStat.Agi, GetData(PlayerStat.Agi));
        SetStat(CharacterStat.Vit, GetData(PlayerStat.Vit));
        SetStat(CharacterStat.Int, GetData(PlayerStat.Int));
        SetStat(CharacterStat.Dex, GetData(PlayerStat.Dex));
        SetStat(CharacterStat.Luk, GetData(PlayerStat.Luk));

        RefreshJobBonus();

        //updated aspd chart
        //base attack speed is identical to pre-renewal, 0.4% lower delay per point of agi and 0.1% for dex
        //the aspd bonus is handled differently should work out to nearly the same up to +60% aspd
        //above that, there's diminishing returns.
        //For example, 0agi/dex +80% apsd (berserk pot/2hq/frenzy) goes from 4.34/sec to 3.54/sec

        var jobAspd = jobInfo.WeaponTimings[Equipment.MainHandWeapon.WeaponClass];
        if (Equipment.IsDualWielding)
            jobAspd = (jobAspd + jobInfo.WeaponTimings[Equipment.OffHandWeapon.WeaponClass]) * 0.7f;
        var aspdBonus = (float)GetStat(CharacterStat.AspdBonus);
        if (aspdBonus >= 0) aspdBonus *= MathF.Pow(1.0064f, aspdBonus);

        var agi = GetEffectiveStat(CharacterStat.Agi);
        var dex = GetEffectiveStat(CharacterStat.Dex);

        var speedScore = 1 - (agi + dex / 4f) / 250f;
        var speedBoost = MathF.Pow(0.99f, aspdBonus);
        var recharge = jobAspd * speedScore * speedBoost;

        //--- old formula -------------------------------------------------------
        /*
        var jobAspd = jobInfo.WeaponTimings[WeaponClass];
        var aspdBonus = 100f / (100f + float.Clamp(GetStat(CharacterStat.AspdBonus), -99, 1000));

        var agi = GetEffectiveStat(CharacterStat.Agi);
        var dex = GetEffectiveStat(CharacterStat.Dex);

        // Trust me this works. I think!
        var speedScore = (agi + dex / 4) * 5 / 3; //agi * 1.6667
        var speedBoost = 1 + ((MathHelper.PowScaleUp(speedScore) - 1) / 4.8f);
        var statSpeedValue = 1f / speedBoost;
        
        var recharge = jobAspd * aspdBonus * statSpeedValue;
        */
        //--- end old formula -------------------------------------------------------

        if (recharge > 2f)
            recharge = 2f;

        var motionTime = 1f;
        var spriteTime = 0.6f;
        if (WeaponClass == 12) //bow
        {
            motionTime = recharge * 0.75f;
            spriteTime = recharge * 0.75f;
        }

        if (recharge < motionTime)
        {
            var ratio = recharge / motionTime;
            motionTime *= ratio;
            spriteTime *= ratio;
        }

        var hitDelayTime = float.Clamp(0.5f - agi * 0.004f, 0.2f, 0.6f); //originally was 0.288f

        SetTiming(TimingStat.AttackDelayTime, recharge);
        SetTiming(TimingStat.AttackMotionTime, motionTime);
        SetTiming(TimingStat.SpriteAttackTiming, spriteTime);
        SetTiming(TimingStat.HitDelayTime, hitDelayTime);

        var hpPercent = 100 + GetStat(CharacterStat.AddMaxHpPercent);

        var newMaxHp = DataManager.JobMaxHpLookup[job][level] * (1 + GetEffectiveStat(CharacterStat.Vit) / 100f);
        newMaxHp += GetStat(CharacterStat.AddMaxHp);
        var updatedMaxHp = newMaxHp * hpPercent / 100;

        SetStat(CharacterStat.MaxHp, updatedMaxHp);
        if (GetStat(CharacterStat.Hp) <= 0 && Character.State != CharacterState.Dead)
            SetStat(CharacterStat.Hp, updatedMaxHp);
        if (GetStat(CharacterStat.Hp) > updatedMaxHp)
            SetStat(CharacterStat.Hp, updatedMaxHp);

        var spPercent = 100 + GetStat(CharacterStat.AddMaxSpPercent);
        var newMaxSp = DataManager.JobMaxSpLookup[job][level] * (1 + GetEffectiveStat(CharacterStat.Int) / 100f);
        newMaxSp += GetStat(CharacterStat.AddMaxSp);
        newMaxSp = newMaxSp * spPercent / 100;

        SetStat(CharacterStat.MaxSp, newMaxSp);
        if (GetStat(CharacterStat.Sp) > newMaxSp)
            SetStat(CharacterStat.Sp, newMaxSp);

        var weightBonus = MaxLearnedLevelOfSkill(CharacterSkill.EnlargeWeightLimit) * 2000;
        SetStat(CharacterStat.WeightCapacity, 28000 + GetEffectiveStat(CharacterStat.Str) * 300 + weightBonus);

        var moveBonus = 1f;
        if (CombatEntity.HasBodyState(BodyStateFlags.Curse))
            moveBonus = 1 / 0.1f;
        else
        {
            moveBonus = 100f / (100f + float.Clamp(GetStat(CharacterStat.MoveSpeedBonus), -99, 500));
            if (moveBonus < 0.8f)
                moveBonus = 0.8f; //lower is faster, speed here is capped at +20%. Later peco will push this limit to 0.7 (+30%)
            if (HasCart)
                moveBonus += 0.05f * (10 - MaxLearnedLevelOfSkill(CharacterSkill.PushCart));
        }

        //var moveSpeed = 0.15f - (0.001f * level / 5f);
        var oldMoveSpeed = Character.MoveSpeed;

        var moveSpeed = 0.15f * moveBonus;
        SetTiming(TimingStat.MoveSpeed, moveSpeed);
        Character.MoveSpeed = moveSpeed;

        //status effect stuff

        OnMeleeAttackStatusFlags = 0;
        OnRangedAttackStatusFlags = 0;
        OnMeleeAttackStatusSelfFlags = 0;
        WhenAttackedStatusFlags = 0;
        OnAttackTriggerFlags = 0;

        foreach (var (effect, val) in PlayerStatData)
        {
            if (val == 0)
                continue;

            switch (effect)
            {
                case >= CharacterStat.OnMeleeAttackFirst and <= CharacterStat.OnMeleeAttackLast:
                    OnMeleeAttackStatusFlags |= (StatusTriggerFlags)(1 << (effect - CharacterStat.OnMeleeAttackFirst));
                    break;
                case >= CharacterStat.OnRangedAttackFirst and <= CharacterStat.OnRangedAttackLast:
                    OnRangedAttackStatusFlags |= (StatusTriggerFlags)(1 << (effect - CharacterStat.OnRangedAttackFirst));
                    break;
                case >= CharacterStat.OnMeleeStatusSelfFirst and <= CharacterStat.OnMeleeStatusSelfLast:
                    OnMeleeAttackStatusSelfFlags |= (StatusTriggerFlags)(1 << (effect - CharacterStat.OnMeleeStatusSelfFirst));
                    break;
                case >= CharacterStat.WhenAttackedFirst and <= CharacterStat.WhenAttackedLast:
                    WhenAttackedStatusFlags |= (StatusTriggerFlags)(1 << (effect - CharacterStat.WhenAttackedFirst));
                    break;
                case CharacterStat.PureHpDrain:
                case CharacterStat.HpDrainChance:
                    OnAttackTriggerFlags |= AttackEffectTriggers.HpDrain;
                    break;
                case CharacterStat.PureSpDrain:
                case CharacterStat.SpDrainChance:
                    OnAttackTriggerFlags |= AttackEffectTriggers.SpDrain;
                    break;
                case CharacterStat.HpGainOnAttack:
                case >= CharacterStat.HpGainOnAttackRaceFormless and <= CharacterStat.HpGainOnAttackRaceUndead:
                    OnAttackTriggerFlags |= AttackEffectTriggers.HpOnAttack;
                    break;
                case CharacterStat.SpGainOnAttack:
                case >= CharacterStat.SpGainOnAttackRaceFormless and <= CharacterStat.SpGainOnAttackRaceUndead:
                    OnAttackTriggerFlags |= AttackEffectTriggers.SpOnAttack;
                    break;
                case CharacterStat.HpGainOnKill:
                case >= CharacterStat.HpGainOnKillRaceFormless and <= CharacterStat.HpGainOnKillRaceUndead:
                    OnAttackTriggerFlags |= AttackEffectTriggers.HpOnKill;
                    break;
                case CharacterStat.SpGainOnKill:
                case >= CharacterStat.SpGainOnKillRaceFormless and <= CharacterStat.SpGainOnKillRaceUndead:
                    OnAttackTriggerFlags |= AttackEffectTriggers.SpOnKill;
                    break;
                case CharacterStat.KnockOutOnAttack:
                case >= CharacterStat.KnockOutOnAttackRaceFormless and <= CharacterStat.KnockOutOnAttackRaceUndead:
                    OnAttackTriggerFlags |= AttackEffectTriggers.KillOnAttack;
                    break;
            }
        }

        //update skill points! Ideally, this only should happen when you change your skills, but, well...
        var jobLevel = GetData(PlayerStat.JobLevel);
        if (JobSkillTree != null)
        {
            var skillPointEarned = JobSkillTree.PrereqSkillPoints + (jobLevel - 1); // job == 0 ? jobLevel - 1 : jobLevel + 9 - 1;

            if (job == 0 && skillPointEarned > 9)
                skillPointEarned = 9;

            if (ServerConfig.DebugConfig.UnlimitedSkillPoints)
                skillPointEarned = 999;

            var SkillPointsUsed = 0;
            foreach (var skill in LearnedSkills)
                SkillPointsUsed += skill.Value;

            if (skillPointEarned < SkillPointsUsed)
                SetData(PlayerStat.SkillPoints, 0);
            else
                SetData(PlayerStat.SkillPoints, skillPointEarned - SkillPointsUsed);
        }

        //update stat points! Probably also should only happen when you change your stat points.
        var statPointsEarned = statPointsEarnedByLevel[level - 1];
        var statPointsUsed = 0;
        for (var i = 0; i < 6; i++)
        {
            var statVal = MathHelper.Clamp(GetData(PlayerStat.Str + i), 1, 99);

            statPointsUsed += cumulativeStatPointCost[statVal - 1];
        }

        SetData(PlayerStat.StatPoints, statPointsEarned - statPointsUsed);

        if (Connection.IsConnectedAndInGame && sendUpdate)
        {
            CommandBuilder.SendUpdatePlayerData(this, false, updateSkillData);
            if (Character.IsMoving && Math.Abs(oldMoveSpeed - Character.MoveSpeed) > 0.03)
                Character.TryMove(Character.TargetPosition, 0);
        }
    }

    public void RefreshWeaponMastery()
    {
        var mastery = 0;
        switch (WeaponClass)
        {
            case 1: //dagger
            case 2: //sword
                mastery = MaxLearnedLevelOfSkill(CharacterSkill.SwordMastery) * 4;
                break;
            case 3: //2hand sword
                mastery = MaxLearnedLevelOfSkill(CharacterSkill.TwoHandSwordMastery) * 4;
                break;
            case 8:
            case 9:
                mastery = MaxLearnedLevelOfSkill(CharacterSkill.MaceMastery) * 4;
                break;
            case 16:
                mastery = MaxLearnedLevelOfSkill(CharacterSkill.KatarMastery) * 4;
                break;
        }

        var appraisal = MaxLearnedLevelOfSkill(CharacterSkill.ItemAppraisal);
        if (appraisal > 0)
            mastery += appraisal * Equipment.MainHandWeapon.WeaponLevel * 2;

        SetStat(CharacterStat.WeaponMastery, mastery);
    }

    public void LevelUp()
    {
        var level = GetData(PlayerStat.Level);

        if (level + 1 > 99)
            return; //hard lock levels above 99

        level++;

        SetData(PlayerStat.Level, level);
        SetStat(CharacterStat.Level, level);

        RefreshPassiveSkills();
        UpdateStats();

        CombatEntity.FullRecovery(true, true);

        if (OnLevelUpEvent != null)
            OnLevelUpEvent(this);
    }

    public void JumpToLevel(int target)
    {
        var level = GetData(PlayerStat.Level);

        if (target < 1 || target > 99)
            return; //hard lock levels above 99

        level = target;

        SetData(PlayerStat.Level, level);
        SetData(PlayerStat.Experience, 0); //reset exp to 0
        SetStat(CharacterStat.Level, level);

        RefreshPassiveSkills();
        UpdateStats();

        if (OnLevelUpEvent != null)
            OnLevelUpEvent(this);

        CombatEntity.FullRecovery(true, true);
    }

    //sometimes we want to change the player's state when they start casting something
    public void NotifyOfSkillCastAttempt(CharacterSkill skill)
    {
        if (SpecialState == SpecialPlayerActionState.WaitingOnPortalDestination && skill != CharacterSkill.WarpPortal)
        {
            SpecialState = SpecialPlayerActionState.None;
            CommandBuilder.ChangePlayerSpecialActionState(this, SpecialPlayerActionState.None);
        }

        if (skill != CharacterSkill.Cloaking && skill != CharacterSkill.Hiding)
            CombatEntity.UpdateHidingStateAfterAttack();
    }

    public void SaveCharacterToData()
    {
        SetData(PlayerStat.Hp, GetStat(CharacterStat.Hp));
        SetData(PlayerStat.Mp, GetStat(CharacterStat.Sp));
    }

    public void ApplyDataToCharacter()
    {
        SetStat(CharacterStat.Hp, GetData(PlayerStat.Hp));
        SetStat(CharacterStat.Sp, GetData(PlayerStat.Mp));
    }

    public void EndNpcInteractions()
    {
        if (!IsInNpcInteraction)
            return;

        NpcInteractionState.CancelInteraction();
    }

    [ScriptUseable]
    public void ReturnToSavePoint()
    {
        Debug.Assert(Character.Map != null);
        SpecialState = SpecialPlayerActionState.None;

        var savePoint = SavePosition.MapName;
        var position = SavePosition.Position;
        var area = SavePosition.Area;
        if (!World.Instance.TryGetWorldMapByName(savePoint, out var targetMap))
        {
            ServerLogger.LogWarning($"Could not return player {Name} to save point {savePoint}.");
            if (!World.Instance.TryGetWorldMapByName(ServerConfig.EntryConfig.Map, out targetMap))
                return;
            position = ServerConfig.EntryConfig.Position;
            area = ServerConfig.EntryConfig.Area;
        }

        Character.StopMovingImmediately();

        if (!WarpPlayer(savePoint, position.X, position.Y, area, area, false))
            ServerLogger.LogWarning($"Failed to move player via ReturnToSavePoint to {savePoint}!");

        AddInputActionDelay(InputActionCooldownType.Teleport);
    }

    public bool HasSpForSkill(CharacterSkill skill, int level)
    {
        var spCost = GetSpCostForSkill(skill, level);
        if (spCost == 0)
            return true;

        var currentSp = GetStat(CharacterStat.Sp);
        if (currentSp < spCost)
            return false;

        return true;
    }

    public int GetSpCostForSkill(CharacterSkill skill, int level)
    {
        if (!SkillHandler.ShouldSkillCostSp(skill, CombatEntity))
            return 0;

        return DataManager.GetSpForSkill(skill, level) * (100 + GetStat(CharacterStat.SpConsumption)) / 100;
    }

    public void TakeSpValue(int spCost)
    {
        var currentSp = GetStat(CharacterStat.Sp);
        if (currentSp < spCost)
            spCost = currentSp;

        currentSp -= spCost;
        SetStat(CharacterStat.Sp, currentSp);
        CommandBuilder.ChangeSpValue(this, currentSp, GetStat(CharacterStat.MaxSp));
        if (Party != null)
            CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(this, true);
    }

    public bool TryTakeSpValue(int spCost)
    {
        var currentSp = GetStat(CharacterStat.Sp);
        if (currentSp < spCost)
            return false;

        TakeSpValue(spCost);
        return true;
    }

    public void AddSkillToCharacter(CharacterSkill skill, int level)
    {
        if (LearnedSkills.TryGetValue(skill, out var curLevel))
            SkillHandler.RemovePassiveEffects(skill, CombatEntity, curLevel); //remove the old passive if they currently know it

        SkillHandler.ApplyPassiveEffects(skill, CombatEntity, level);
        LearnedSkills[skill] = level;

        if (SkillHandler.GetSkillAttributes(skill).SkillTarget == SkillTarget.Passive)
            UpdateStats();
    }

    public void GrantSkillToCharacter(CharacterSkill skill, int level)
    {
        GrantedSkills ??= new();

        if (GrantedSkills.TryGetValue(skill, out var cur))
        {
            if (cur >= level)
                return;

            GrantedSkills[skill] = level;
            return;
        }

        GrantedSkills.Add(skill, level);
    }

    public void RemoveGrantedSkill(CharacterSkill skill, int level = -1)
    {
        if (GrantedSkills == null)
            return;

        if (!GrantedSkills.TryGetValue(skill, out var cur))
            return;

        if (level > 0 && cur != level)
            return;

        GrantedSkills.Remove(skill);
    }

    private void RefreshPassiveSkills()
    {
        foreach (var skill in LearnedSkills)
            SkillHandler.RefreshPassiveEffects(skill.Key, CombatEntity, skill.Value);

        if (GrantedSkills == null)
            return;

        foreach (var skill in GrantedSkills)
            SkillHandler.RefreshPassiveEffects(skill.Key, CombatEntity, skill.Value);
    }

    public void UpdateSitStatus(bool isSitting)
    {
        if (!isSitting)
        {
            isSittingHpTick = false;
            isSittingSpTick = false;
        }
    }

    private void UpdateRegenTick()
    {
        switch (Character.State)
        {
            case CharacterState.Dead:
                return;
            case CharacterState.Sitting:
                regenHpTickTime -= Time.DeltaTimeFloat * 2;
                regenSpTickTime -= Time.DeltaTimeFloat * 2;
                break;
            case CharacterState.Moving:
                regenHpTickTime -= Time.DeltaTimeFloat / 2f;
                regenSpTickTime -= Time.DeltaTimeFloat;
                break;
            default:
                regenHpTickTime -= Time.DeltaTimeFloat;
                regenSpTickTime -= Time.DeltaTimeFloat;
                break;
        }

        if (regenHpTickTime < 0)
        {
            HpRegenTick();
            regenHpTickTime += HpRegenTickTime;
        }

        if (regenSpTickTime < 0)
        {
            SpRegenTick();
            regenSpTickTime += SpRegenTickTime;
        }
    }

    private void HpRegenTick()
    {
        if (!Character.IsActive || Character.State == CharacterState.Dead)
            return;

        var hp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);
        var hpAddPercent = 100 + GetStat(CharacterStat.AddHpRecoveryPercent);

        if (hp >= maxHp || hpAddPercent <= 0) return;

        var vit = GetEffectiveStat(CharacterStat.Vit);
        var regen = (maxHp / 50 + vit / 5) * (200 + vit) / 200;
        regen = regen * hpAddPercent / 100;

        if (Character.State == CharacterState.Sitting)
        {
            if (isSittingHpTick)
                regen *= 2;
            else
                isSittingHpTick = true; //first hp tick when sitting isn't doubled. Prevents sitting quickly to catch the tick bonus.
        }
        else
            isSittingHpTick = false;

        var hpRegenSkill = MaxLearnedLevelOfSkill(CharacterSkill.IncreasedHPRecovery);
        if (hpRegenSkill > 0 && hp < maxHp)
        {
            var plusHpRegen = 5 * hpRegenSkill + maxHp * hpRegenSkill / 500;
            regen += plusHpRegen;
            CommandBuilder.SendImprovedRecoveryValue(this, plusHpRegen, 0);
        }

        if (regen < 1) regen = 1;

        if (regen + hp > maxHp)
            regen = maxHp - hp;

        SetStat(CharacterStat.Hp, hp + regen);

        if (regen > 0)
        {
            //Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            //CommandBuilder.SendHealSingle(this, regen, HealType.None);
            CommandBuilder.SendHealMultiAutoVis(Character, regen, HealType.None);
            //CommandBuilder.ClearRecipients();

            if (Party != null)
                CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(this, false); //only update those out of range
        }
    }

    private void SpRegenTick()
    {
        var sp = GetStat(CharacterStat.Sp);
        var maxSp = GetStat(CharacterStat.MaxSp);
        var spAddPercent = 100 + GetStat(CharacterStat.AddSpRecoveryPercent);

        if (sp >= maxSp || spAddPercent <= 0) return;

        var chInt = GetEffectiveStat(CharacterStat.Int);
        var regen = 1 + (maxSp / 100 + chInt / 6) * (200 + chInt) / 200;
        //var regen = maxSp / 100 + chInt / 5; //original formula

        if (chInt >= 120) regen += chInt / 2 - 56;
        regen = regen * spAddPercent / 100;

        if (Character.State == CharacterState.Sitting)
        {
            if (isSittingSpTick)
                regen *= 2;
            else
                isSittingSpTick = true;
        }
        else
            isSittingSpTick = false;

        var spRegenSkill = MaxLearnedLevelOfSkill(CharacterSkill.IncreaseSPRecovery);
        if (spRegenSkill > 0 && sp < maxSp)
        {
            var plusSpRegen = 3 * spRegenSkill + maxSp * spRegenSkill / 500; //3sp + 0.2% maxSP per level
            regen += plusSpRegen;
            CommandBuilder.SendImprovedRecoveryValue(this, 0, plusSpRegen);
        }

        if (regen < 1)
            regen = 1;

        if (regen + sp > maxSp)
            regen = maxSp - sp;

        SetStat(CharacterStat.Sp, sp + regen);
        if (regen > 0)
        {
            CommandBuilder.ChangeSpValue(this, sp + regen, maxSp);
            if (Party != null)
                CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(this, true);
        }
    }

    public void Die()
    {
        if (Character.Map == null)
            throw new Exception("Attempted to kill a player, but the player is not attached to any map.");

        if (Character.State == CharacterState.Dead)
            return; //we're already dead!

        if (NpcInteractionState.InteractionResult == NpcInteractionResult.CurrentlyVending)
        {
            if (NpcInteractionState.NpcEntity.TryGet<Npc>(out var npc))
            {
                CommandBuilder.VendingEnd(this);
                npc.EndEvent();
                NpcInteractionState.CancelInteraction();
            }
        }

        ClearTarget();
        EndNpcInteractions();
        Character.StopMovingImmediately();
        Character.State = CharacterState.Dead;
        Character.QueuedAction = QueuedAction.None;
        Character.InMoveLock = false;
        CombatEntity.IsCasting = false;
        CombatEntity.CastingSkill.Clear();
        CombatEntity.QueuedCastingSkill.Clear();
        CombatEntity.OnDeathClearStatusEffects();
        UpdateStats();
        SpecialState = SpecialPlayerActionState.None;

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendPlayerDeath(Character);
        CommandBuilder.ClearRecipients();
    }

    private bool ValidateTarget()
    {
        if (Target.IsNull() || !Target.IsAlive())
        {
            ClearTarget();
            return false;
        }

        if (!Target.TryGet<CombatEntity>(out var ce))
            return false;

        return ce.IsValidTarget(CombatEntity);
    }

    public void ClearTarget()
    {
        AutoAttackLock = false;

        if (!Target.IsNull())
            CommandBuilder.SendChangeTarget(this, null);

        Target = Entity.Null;
    }

    public void ChangeTarget(WorldObject? target)
    {
        if (target == null || Target == target.Entity)
            return;

        CommandBuilder.SendChangeTarget(this, target);

        Target = target.Entity;
    }

    //returns true if the cast was successful, false if it failed OR if the cast was queued
    [ScriptUseable]
    public bool TryCastItemSkill(int itemId, CombatEntity target, CharacterSkill skill, int lvl = 1)
    {
        if (Inventory == null || !Inventory.HasItem(itemId))
            return false;

        var castTime = SkillHandler.GetSkillCastTime(skill, CombatEntity, target, lvl);

        CombatEntity.AttemptStartSingleTargetSkillAttack(target, skill, lvl, castTime, SkillCastFlags.None, itemId);

        return false; //we always return false, the skill activating will consume the item
    }

    public int DefaultWeaponForJob(int newJobId) => newJobId switch
    {
        0 => 1, //novice => dagger
        1 => 2, //swordsman => sword
        2 => 12, //archer => bow
        3 => 10, //mage => rod
        4 => 8, //acolyte => mace
        5 => 1, //thief => dagger
        6 => 6, //merchant => axe
        _ => 1, //anything else => dagger
    };

    public void RefreshJobBonus()
    {
        var bonusData = DataManager.GetJobBonusesForLevel(GetData(PlayerStat.Job), GetData(PlayerStat.JobLevel));

        if (jobStatBonuses != null)
        {
            if (bonusData.Equals(jobStatBonuses.Value))
                return;

            var existing = jobStatBonuses.Value.Span;
            for (var i = 0; i < 6; i++)
                SubStat(CharacterStat.AddStr + i, existing[i]);
        }

        var newBonuses = bonusData.Span;

        for (var i = 0; i < 6; i++)
            AddStat(CharacterStat.AddStr + i, newBonuses[i]);

        jobStatBonuses = bonusData;
    }

    public void ChangeJob(int newJobId)
    {
        var curJob = GetData(PlayerStat.Job);
        SetData(PlayerStat.Job, newJobId);
        if (curJob == 0)
        {
            SetData(PlayerStat.JobLevel, 1); //only reset job if they're changing from novice. Will need to change later.
            SetData(PlayerStat.JobExp, 0);
        }
        else
            CombatEntity.StatusContainer?.RemoveAll();

        if (Character.ClassId < 100) //we don't want to override special character classes like GameMaster
            Character.ClassId = newJobId;

        PlayerFollower &= ~PlayerFollower.AnyCart;
        SetData(PlayerStat.FollowerType, 0);

        Equipment.UnequipAllItems();
        UpdateStats();

        if (Character.Map != null)
            Character.Map.RefreshEntity(Character);
    }

    [ScriptUseable]
    public void SaveSpawnPoint(string spawnName)
    {
        if (DataManager.SavePoints.TryGetValue(spawnName, out var spawnPosition))
        {
            ServerLogger.Log($"Player setting spawn point to {spawnName} (map {spawnPosition.MapName})");
            SavePosition = spawnPosition;
            WriteCharacterToDatabase(); //save the new point
        }
        else
            ServerLogger.LogError($"Npc script attempted to set spawn position to \"{spawnName}\", but that spawn point was not defined.");
    }

    [ScriptUseable]
    public bool CanUseSummonItem(int count = 1)
    {
        return Inventory == null || Inventory.UsedSlots + count < CharacterBag.MaxBagSlots;
    }

    [ScriptUseable]
    public bool CanUseItemSkill()
    {
        if (!Character.StateCanAttack)
            return false;
        if (GetStat(CharacterStat.Disabled) > 0 || CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return false;
        return true;
    }

    [ScriptUseable]
    public void ItemActivatedSkillSelfTarget(CharacterSkill skill, int level)
    {
        var cast = new SkillCastInfo()
        {
            CastTime = 0,
            Flags = SkillCastFlags.None,
            IsIndirect = true,
            Level = level,
            Range = 0,
            Skill = skill,
            TargetEntity = Entity,
            TargetedPosition = Position.Invalid
        };
        Character.StopMovingImmediately();
        SkillHandler.ExecuteSkill(cast, CombatEntity);
    }

    [ScriptUseable]
    public void UseItemCreationItem(string type)
    {
        if (!DataManager.ItemBoxSummonList.TryGetValue(type, out var list))
        {
            ServerLogger.LogWarning($"Failed to perform UseItemCreationItem for item type {type}, type not found in ItemBoxSummonList table!");
            return;
        }

        var selType = list[GameRandom.Next(0, list.Count)];
        CreateItemInInventory(new ItemReference(selType, 1));


        ////0 is obb, 1 is ovb, 2 is oca
        ////this is obviously broken but mostly just for fun until it gets a proper implementation
        //switch (type)
        //{
        //    case 0:
        //        {
        //            //create 1 of any item in the game
        //            var item = DataManager.ItemList.ElementAt(GameRandom.Next(0, DataManager.ItemList.Count));
        //            CreateItemInInventory(new ItemReference(item.Key, 1));
        //            return;
        //        }
        //    case 1:
        //        {
        //            //create 1 of any weapon or armor
        //            var totalCount = DataManager.WeaponInfo.Count + DataManager.ArmorInfo.Count;
        //            var sel = GameRandom.Next(0, totalCount);
        //            if (sel < DataManager.WeaponInfo.Count)
        //            {
        //                sel = GameRandom.Next(0, DataManager.WeaponInfo.Count);
        //                var item = DataManager.WeaponInfo.ElementAt(sel);
        //                CreateItemInInventory(new ItemReference(item.Key, 1));
        //            }
        //            else
        //            {
        //                sel = GameRandom.Next(0, DataManager.ArmorInfo.Count);
        //                var item = DataManager.ArmorInfo.ElementAt(sel);
        //                CreateItemInInventory(new ItemReference(item.Key, 1));
        //            }

        //            return;
        //        }
        //    case 2:
        //        {
        //            //create 1 of any card
        //            var item = DataManager.CardInfo.ElementAt(GameRandom.Next(0, DataManager.CardInfo.Count));
        //            CreateItemInInventory(new ItemReference(item.Key, 1));
        //            return;
        //        }
        //}
    }

    [ScriptUseable]
    public void UseSummonItem(string itemName, int lifetime = int.MaxValue)
    {
        Debug.Assert(Character.Map != null);

        if (!DataManager.ItemMonsterSummonList.TryGetValue(itemName, out var monList))
        {
            ServerLogger.LogWarning($"Attempting to UseSummonItem with itemName of {itemName}, but that summon item type could not be found.");
            return;
        }

        var idx = GameRandom.Next(0, monList.Count);
        var monDef = DataManager.MonsterCodeLookup[monList[idx]];
        var entity = World.Instance.CreateMonster(Character.Map, monDef, Area.CreateAroundPoint(Character.Position, 7), null);
        var monster = entity.Get<Monster>();
        if (lifetime < int.MaxValue)
            monster.CombatEntity.AddStatusEffect(CharacterStatusEffect.Doom, lifetime);
        monster.ChangeAiStateMachine(MonsterAiType.AiStandardBoss);
        monster.ResetAiUpdateTime(); //make it active instantly
    }

    private bool ValidateAmmoBasedWeapon()
    {
        if (Equipment.AmmoId <= 0) //bow
        {
            CommandBuilder.SendServerEvent(this, ServerEvent.NoAmmoEquipped);
            return false;
        }

        if (WeaponClass == 12 && Equipment.AmmoType != AmmoType.Arrow)
        {
            CommandBuilder.SendServerEvent(this, ServerEvent.WrongAmmoEquipped);
            return false;
        }

        if (Inventory == null || !Inventory.HasItem(Equipment.AmmoId))
        {
            CommandBuilder.SendServerEvent(this, ServerEvent.OutOfAmmo);
            return false;
        }

        return true;
    }

    private void PerformQueuedAttack()
    {
        if (Character.State == CharacterState.Sitting
            || Character.State == CharacterState.Dead
            || (CombatEntity.BodyState & BodyStateFlags.NoAutoAttack) > 0
            || CombatEntity.HasBodyState(BodyStateFlags.Pacification)
            || GetStat(CharacterStat.Disabled) > 0
            || !ValidateTarget())
        {
            AutoAttackLock = false;
            return;
        }

        var usingAmmo = false;
        if (WeaponClass == 12)
        {
            usingAmmo = true;
            if (!ValidateAmmoBasedWeapon())
            {
                AutoAttackLock = false;
                return;
            }
        }

        var targetCharacter = Target.Get<WorldObject>();
        if (!targetCharacter.IsActive || targetCharacter.Map != Character.Map || targetCharacter.CombatEntity.HasFatalDamageQueued())
        {
            AutoAttackLock = false;
            return;
        }

        if (DistanceCache.IntDistance(Character.Position, targetCharacter.Position) > GetStat(CharacterStat.Range))
        {
            if (InMoveReadyState)
            {
                if (!Character.TryMove(targetCharacter.Position, 1))
                    AutoAttackLock = false;
            }

            return;
        }

        if (Character.State == CharacterState.Moving)
        {
            Character.StopMovingImmediately();
            //if (Character.StepsRemaining > 1)
            //    Character.ShortenMovePath(); //no point in shortening a path that is already short

            //return;
        }

        ChangeTarget(targetCharacter);
        if (PerformAttack(targetCharacter))
        {
            if (usingAmmo && Inventory!.RemoveItemByBagId(Equipment.AmmoId, 1))
            {
                CommandBuilder.RemoveItemFromInventory(this, Equipment.AmmoId, 1);
            }

        }
    }

    public DamageInfo CalculateMeleeAttack(CombatEntity target)
    {
        var multiplier = 1f;
        var isDualWielding = Character.Player.Equipment.IsDualWielding;

        if (isDualWielding)
            multiplier = 0.5f + MaxLearnedLevelOfSkill(CharacterSkill.RightHandMastery) * 0.1f;

        var di = CombatEntity.CalculateCombatResult(target, multiplier, 1, AttackFlags.Physical | AttackFlags.CanCrit);
        var canDoubleAttack = (Character.Player.WeaponClass == 1 ||
                               Character.Player.Equipment.DoubleAttackModifiers > 0);

        if (canDoubleAttack)
        {
            var doubleChance = GetStat(CharacterStat.DoubleAttackChance);
            if (doubleChance > 0 && GameRandom.Next(0, 100) <= doubleChance)
                di.HitCount = 2;
        }

        if (WeaponClass == 16 && di.Damage > 0) //katar
        {
            di.Time = Time.ElapsedTimeFloat + di.TimeInSeconds * 0.5f;
            di.DamageOffHand = (short)(di.Damage * (1 + MaxAvailableLevelOfSkill(CharacterSkill.DoubleAttack) * 2) / 100);
            if (di.DamageOffHand <= 0)
                di.DamageOffHand = 1;
        }

        //add offhand weapon attack
        if (isDualWielding)
        {
            di.Time = Time.ElapsedTimeFloat + di.TimeInSeconds * 0.5f;

            var flags = AttackFlags.Physical | AttackFlags.NoTriggers | AttackFlags.OffHandWeapon | AttackFlags.IgnoreNullifyingGroundMagic | AttackFlags.NoDamageModifiers;
            if (di.Result == AttackResult.CriticalDamage)
                flags |= AttackFlags.GuaranteeCrit; //if the original crits, offhand crits, otherwise, it doesn't
            var element = Equipment.OffHandWeapon.WeaponElement;
            if (element == AttackElement.None)
                element = AttackElement.Neutral; //None would be replaced by main-hand element, and off-hand is never endowed, so we specify here if it's a neutral type.

            var req = new AttackRequest(0.3f + MaxLearnedLevelOfSkill(CharacterSkill.LeftHandMastery) * 0.1f, 1, flags, element);
            (req.MinAtk, req.MaxAtk) = CombatEntity.CalculateAttackPowerRange(false, false); //calculate offhand
            var di2 = CombatEntity.CalculateCombatResult(target, req);

            if (di2.IsDamageResult)
                di.DamageOffHand = (short)di2.Damage;
            else
                di.DamageOffHand = -1;
        }

        return di;
    }

    private bool PerformAttack(WorldObject targetCharacter)
    {
        if (targetCharacter.Type == CharacterType.NPC || Character.Map == null)
        {
            ChangeTarget(null);

            return false;
        }

        var targetEntity = targetCharacter.Entity.Get<CombatEntity>();
        if (!targetEntity.IsValidTarget(CombatEntity) || targetEntity.IsHiddenTo(CombatEntity)
                                                      || !Character.Map.WalkData.HasLineOfSight(Character.Position, targetCharacter.Position))
        {
            ClearTarget();
            return false;
        }

        AutoAttackLock = true;

        if (Character.State == CharacterState.Moving)
        {
            if (Character.QueuedAction == QueuedAction.Move && Character.MoveLockTime > Time.DeltaTimeFloat)
                Character.State = CharacterState.Idle;
            else
                Character.ShortenMovePath();

            if (Target != targetCharacter.Entity)
                ChangeTarget(targetCharacter);

            return false;
        }

        //Character.StopMovingImmediately();

        if (Character.AttackCooldown > Time.ElapsedTime)
        {
            if (Target != targetCharacter.Entity)
                ChangeTarget(targetCharacter);

            return false;
        }

        Character.ResetSpawnImmunity();
        CombatEntity.PerformMeleeAttack(targetEntity);
        Character.AddMoveLockTime(GetTiming(TimingStat.AttackMotionTime), true);

        Character.AttackCooldown = Time.ElapsedTime + GetTiming(TimingStat.AttackDelayTime);
        return true;
    }

    public void TargetForAttack(WorldObject enemy)
    {
        var usingAmmo = false;
        if (WeaponClass == 12)
        {
            usingAmmo = true;
            if (!ValidateAmmoBasedWeapon())
                return;
        }

        if (CombatEntity.IsCasting)
        {
            ChangeTarget(enemy);
            AutoAttackLock = true;
            return;
        }

        var range = int.Max(1, GetStat(CharacterStat.Range));

        if (DistanceCache.IntDistance(Character.Position, enemy.Position) <= range)
        {
            ChangeTarget(enemy);
            //PerformQueuedAttack();
            if (PerformAttack(enemy))
            {
                if (usingAmmo && Inventory!.RemoveItemByBagId(Equipment.AmmoId, 1))
                {
                    CommandBuilder.RemoveItemFromInventory(this, Equipment.AmmoId, 1);
                }
            }
            return;
        }

        if (!Character.TryMove(enemy.Position, 1))
            return;

        ChangeTarget(enemy);
        AutoAttackLock = true;
    }

    public bool VerifyCanUseSkill(CharacterSkill skill, int lvl)
    {
        return true; //lol
    }

    public bool HasLootPriority(GroundItem item)
    {
        if (item.ContributorId > 0 && Character.Id != item.ContributorId && item.ExclusiveTime > Time.ElapsedTimeFloat)
        {
            var dropOwner = World.Instance.GetEntityById(item.ContributorId);

            if (dropOwner.IsAlive() && dropOwner.TryGet<Player>(out var prioPlayer))
            {
                if (prioPlayer.Party == null || Party == null ||
                    prioPlayer.Party.PartyId != Party.PartyId)
                {
                    CommandBuilder.ErrorMessage(this, "You are unable to pick up this item yet.");
                    return false;
                }
            }
        }

        return true;
    }

    public bool TryPickup(GroundItem groundItem)
    {
        var item = groundItem.ToItemReference();

        if (!CanPickUpItem(item) || !HasLootPriority(groundItem))
            return false;

        Character.Map!.PickUpOrRemoveItem(Character, groundItem.Id);
        Character.AttackCooldown = Time.ElapsedTimeFloat + 0.3f; //no attacking for 0.3s after picking up an item
        CreateItemInInventory(item);
        return true;
    }

    [ScriptUseable]
    public void SendItemUseFailMessage(string message)
    {
        CommandBuilder.ErrorMessage(this, message);
    }

    [ScriptUseable]
    public bool CanOpenItemPackage(string packagedItem, int count)
    {
        if (!DataManager.ItemIdByName.TryGetValue(packagedItem, out var itemId))
            return false;
        var item = new ItemReference(itemId, count);
        return CanPickUpItem(item);
    }

    [ScriptUseable]
    public void OpenItemPackage(string packagedItem, int count)
    {
        if (!DataManager.ItemIdByName.TryGetValue(packagedItem, out var itemId))
        {
            ServerLogger.LogWarning($"Player {Name} attempted to OpenItemPackage to get a {packagedItem}");
            return;
        }

        CreateItemInInventory(new ItemReference(itemId, count));
    }

    public bool CreateItemInInventory(ItemReference item)
    {
        var change = item.Count;
        var updatedCount = (short)AddItemToInventory(item);
        var bagId = item.Id;
        if (item.Type == ItemType.RegularItem)
            item.Item.Count = updatedCount; //AddItemToInventory returns the updated count for regular items
        else
            bagId = updatedCount; //AddItemToInventory returns the bagId for unique items (yeah this is scuffed)
        CommandBuilder.AddItemToInventory(this, item, bagId, change);

        return true;
    }

    private void AttemptQueuedPickupAction()
    {
        if (Character.Map!.TryGetGroundItemByDropId(Character.ItemTarget, out var groundItem))
        {
            if (Character.Position.SquareDistance(groundItem.Position) <= 1)
                TryPickup(groundItem);
        }

        Character.ItemTarget = -1;
        Character.QueuedAction = QueuedAction.None;

        if (!Character.Map.IsEntityStacked(Character))
            return;

        if (Character.Map.FindUnoccupiedAdjacentTile(Character.Position, out var newMove))
            Character.TryMove(newMove, 0);
    }

    public void RegisterRecentAttacker(ref Entity src) => RecentAttackersList.AddOrSetValue(ref src, Time.ElapsedTimeFloat);

    public int GetCurrentAttackerCount()
    {
        if (lastAttackerListCheckUpdate >= Time.ElapsedTimeFloat) //iterating the list doesn't need to be done each time you take damage
            return RecentAttackersList.Count;
        lastAttackerListCheckUpdate = Time.ElapsedTimeFloat + 0.2f;
        return RecentAttackersList.CountEntitiesAboveValueAndRemoveBelow(Time.ElapsedTimeFloat - 1.8f);
    }

    public bool WarpPlayer(string mapName, int x, int y, int width, int height, bool failIfNotWalkable)
    {
        if (!World.Instance.TryGetWorldMapByName(mapName, out var map))
            return false;

        AddInputActionDelay(InputActionCooldownType.Teleport);
        Character.ResetState();
        Character.SetSpawnImmunity();
        Character.StopSitting();

        CombatEntity.ClearDamageQueue();
        CombatEntity.RemoveStatusOfGroupIfExists("StopGroup");
        SpecialState = SpecialPlayerActionState.None;

        var oldPos = Character.Position;
        var p = new Position(x, y);

        if (Character.Map != null && (width > 1 || height > 1))
        {
            var area = Area.CreateAroundPoint(x, y, width, height);
            p = map.GetRandomWalkablePositionInArea(area);
            if (p == Position.Invalid)
            {
                ServerLogger.LogWarning($"Could not warp player to map {mapName} area {area} is blocked.");
                p = new Position(x, y);
            }
        }

        if (Character.Map?.Name == mapName)
        {
            Character.Map.TeleportEntity(ref Entity, Character, p, CharacterRemovalReason.OutOfSight);

            if (CombatEntity.StatusContainer != null)
                CombatEntity.StatusContainer.OnMove(oldPos, p, true);
        }
        else
        {
            oldPos = Position.Invalid;
            World.Instance?.MovePlayerMap(ref Entity, Character, map, p);

            if (CombatEntity.StatusContainer != null)
                CombatEntity.StatusContainer.OnChangeMaps();

            if (Party != null)
            {
                CommandBuilder.UpdatePartyMembersOfMapChange(this, mapName);
                CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(this, true);

            }
        }

        return true;
    }

    public void UpdatePosition()
    {
        if (!ValidateTarget())
            return;

        var targetCharacter = Target.Get<WorldObject>();

        if (Character.State == CharacterState.Moving)
        {
            if (DistanceCache.IntDistance(Character.Position, targetCharacter.Position) <= GetStat(CharacterStat.Range))
                Character.StopMovingImmediately();
        }

        if (Character.State == CharacterState.Idle)
        {
            TargetForAttack(targetCharacter);
        }
    }

    public bool InInputActionCooldown() => InputActionCooldown > 1f;
    public void AddInputActionDelay(InputActionCooldownType type) => InputActionCooldown += InputActionDelay.CooldownTime(type);
    public void AddInputActionDelay(float time) => InputActionCooldown += time;

    private bool InCombatReadyState => (Character.State == CharacterState.Idle || Character.State == CharacterState.Moving)
        && !CombatEntity.IsCasting && Character.AttackCooldown < Time.ElapsedTimeFloat;

    private bool InMoveReadyState => (Character.State == CharacterState.Idle || Character.State == CharacterState.Moving) && !CombatEntity.IsCasting;

    public bool CanPerformCharacterActions(bool ignoreActionCooldown = false)
    {
        if (!ignoreActionCooldown && InInputActionCooldown())
            return false;
        if (Character.State == CharacterState.Dead)
            return false;
        if (CombatEntity.IsCasting)
            return false;
        if (IsInNpcInteraction)
            return false;
        if (GetStat(CharacterStat.Disabled) > 0)
            return false;

        return true;
    }

    public void ApplyAfterCastDelay(float time)
    {
        var openTime = Time.ElapsedTime + time;
        if (openTime < SkillCooldownTime)
            return;
        SkillCooldownTime = openTime;
    }

    private void UpdateWithQueuedCast()
    {
        Debug.Assert(Character.Map != null);

        if (SkillCooldownTime > Time.ElapsedTime)
            return;

        var cast = CombatEntity.QueuedCastingSkill;

        if (IsSkillOnCooldown(cast.Skill)) return;

        if (cast.TargetedPosition != Position.Invalid)
        {
            //targeted at the ground
            var isValid = true;
            var canAttack = cast.TargetedPosition.InRange(Character.Position, cast.Range);
            if (canAttack && !Character.Map.WalkData.HasLineOfSight(Character.Position, cast.TargetedPosition))
                canAttack = false;

            if (Character.State == CharacterState.Moving && canAttack)
                Character.StopMovingImmediately(); //we've locked in place but we're close enough to attack
            if (Character.State == CharacterState.Idle && !canAttack && !Character.InAttackCooldown)
            {
                var target = CombatEntity.CastingSkill.TargetEntity;
                isValid = Character.TryMove(cast.TargetedPosition, 1);
                if (isValid)
                    Character.QueuedAction = QueuedAction.Cast; //trymove will reset this...
            }

            if (InCombatReadyState && isValid && canAttack)
            {
                if (CombatEntity.QueuedCastingSkill.IsValid)
                    CombatEntity.ResumeQueuedSkillAction();
                else
                    Character.QueuedAction = QueuedAction.None;
            }

            if (!isValid)
                Character.QueuedAction = QueuedAction.None;
        }
        else
        {
            //targeted at an enemy
            if (CombatEntity.QueuedCastingSkill.TargetEntity.TryGet<WorldObject>(out var targetCharacter))
            {
                if (targetCharacter.Map != Character.Map)
                {
                    Character.QueuedAction = QueuedAction.None;
                    Target = Entity.Null;
                    return;
                }

                var isValid = true;
                var canAttack = CombatEntity.CanAttackTarget(targetCharacter, CombatEntity.QueuedCastingSkill.Range);
                if (Character.State == CharacterState.Moving && canAttack)
                    Character.StopMovingImmediately(); //we've locked in place but we're close enough to attack
                if (Character.State == CharacterState.Idle && !canAttack)
                {
                    var target = CombatEntity.CastingSkill.TargetEntity;
                    isValid = Character.TryMove(targetCharacter.Position, 1);
                }

                if (InCombatReadyState && isValid)
                {
                    if (CombatEntity.QueuedCastingSkill.IsValid)
                        CombatEntity.ResumeQueuedSkillAction();
                    else
                        Character.QueuedAction = QueuedAction.None;
                }

                if (!isValid)
                    Character.QueuedAction = QueuedAction.None;
            }
            else
            {
                Character.QueuedAction = QueuedAction.None;
                Target = Entity.Null;
            }
        }
    }

    public void IndirectCastQueueUpdate()
    {
        //var hasResult = false;
        while (IndirectCastQueue.Count > 0 && IndirectCastQueue[0].CastTime < Time.ElapsedTimeFloat)
        {
            var cast = IndirectCastQueue[0];
            IndirectCastQueue.RemoveAt(0);

            if (GetStat(CharacterStat.Disabled) > 0 || IsInNpcInteraction)
                continue; //lose the skill activation

            if (SkillHandler.ValidateTarget(cast, CombatEntity, true) == SkillValidationResult.Success)
                SkillHandler.ExecuteSkill(cast, CombatEntity);
        }
    }

    public void Update()
    {
        InputActionCooldown -= Time.DeltaTimeFloat; //this cooldown is the delay on how often a player can perform actions
        if (InputActionCooldown < 0)
            InputActionCooldown = 0;

        Debug.Assert(Character.Map != null);
        Debug.Assert(CombatEntity != null);

        if (!Character.StateCanAttack)
        {
            Character.QueuedAction = QueuedAction.None;
            AutoAttackLock = false;
            if (IndirectCastQueue.Count > 0)
                IndirectCastQueue.Clear();
            if (Character.State == CharacterState.Dead)
                return;
        }

        UpdateRegenTick();

        if (IsInNpcInteraction)
        {
            if (NpcInteractionState.InteractionResult == NpcInteractionResult.WaitForStorageAccess)
            {
                if (Connection.LoadStorageRequest == null)
                {
                    if (StorageInventory != null)
                        NpcInteractionState.FinishOpeningStorage();
                    else
                    {
                        ServerLogger.LogWarning($"Player is waiting for storage to load, but no request sent!");
                        EndNpcInteractions();
                    }
                }
                else
                {
                    if (Connection.LoadStorageRequest.IsComplete)
                    {
                        NpcInteractionState.FinishLoadingStorage(Connection.LoadStorageRequest);
                        Connection.LoadStorageRequest = null;
                    }
                }
            }

            if (!NpcInteractionState.NpcEntity.IsAlive())
                EndNpcInteractions();
        }

        if (Connection.ActiveDbAction != ActiveDbAction.None)
        {
            if (Connection.ActiveDbAction == ActiveDbAction.CreateParty)
            {
                if (Connection.CreatePartyRequest == null || Connection.CreatePartyRequest.HasFailed)
                    Connection.ActiveDbAction = ActiveDbAction.None; //the CreatePartyRequest will probably already have notified failure
                else if (Connection.CreatePartyRequest.IsComplete)
                {
                    if (Connection.CreatePartyRequest.InvitePlayerOnSuccess > 0 && Party != null)
                    {
                        var targetEntity = World.Instance.GetEntityById(Connection.CreatePartyRequest.InvitePlayerOnSuccess);
                        if (targetEntity.TryGet<Player>(out var target))
                            Party.SendInvite(this, target);
                    }

                    Connection.ActiveDbAction = ActiveDbAction.None;
                }
            }
        }

        if (Character.QueuedAction == QueuedAction.Cast)
        {
            if (CombatEntity.HasBodyState(BodyStateFlags.Silence) || SkillCooldownTime > Time.ElapsedTime + 1.5f)
                Character.QueuedAction = QueuedAction.None;
            else
                UpdateWithQueuedCast();
        }

        if (Character.QueuedAction == QueuedAction.Move && InMoveReadyState)
        {
            if (Character.InMoveLock)
                return;

            Character.QueuedAction = QueuedAction.None;
            Character.TryMove(Character.TargetPosition, 0);

            return;
        }

        if (Character.QueuedAction == QueuedAction.PickUpItem && Character.State == CharacterState.Idle && !Character.InAttackCooldown)
        {
            AttemptQueuedPickupAction();
        }

        if (AutoAttackLock)
        {
            if (!Target.TryGet<WorldObject>(out var targetCharacter))
            {
                AutoAttackLock = false;
                Target = Entity.Null;
                return;
            }

            if (Character.InMoveLock && !Character.InAttackCooldown && CombatEntity.CanAttackTarget(targetCharacter))
                Character.StopMovingImmediately();

            if (InCombatReadyState)
                PerformQueuedAttack();
        }

#if DEBUG
        if (Character.Map != null)
        {
            var count = Character.Map.GatherPlayersInRange(Character.Position, ServerConfig.MaxViewDistance, null, false, false);
            if (Character.CountVisiblePlayers() != count)
                ServerLogger.LogWarning($"Player {Character.Name} says it can see {Character.CountVisiblePlayers()} players, but there are {count} players in range.");
        }
#endif
    }
}
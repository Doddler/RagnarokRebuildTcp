using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics.CodeAnalysis;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Util;
using System.Runtime.CompilerServices;
using RoRebuildServer.ScriptSystem;
using System.Runtime.InteropServices;
using RoRebuildServer.Data.Monster;
using System.Text.RegularExpressions;

namespace RoRebuildServer.EntityComponents;

[EntityComponent([EntityType.Player, EntityType.Monster])]
public partial class CombatEntity : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;
    public Player Player = null!;

    public int Faction;
    public int Party;
    public bool IsTargetable;
    public float CastingTime;
    public bool IsCasting;
    public CastInterruptionMode CastInterruptionMode;

    public BodyStateFlags BodyState;

    public SkillCastInfo CastingSkill { get; set; }
    public SkillCastInfo QueuedCastingSkill { get; set; }

    [EntityIgnoreNullCheck]
    private readonly int[] statData = new int[(int)CharacterStat.MonsterStatsMax];

    [EntityIgnoreNullCheck]
    private float[] timingData = new float[(int)TimingStat.TimingStatsMax];

    [EntityIgnoreNullCheck] private Dictionary<CharacterSkill, float> skillCooldowns = new();
    [EntityIgnoreNullCheck] private Dictionary<CharacterSkill, float> damageCooldowns = new();
    [EntityIgnoreNullCheck] public List<DamageInfo> DamageQueue { get; set; } = null!;
    private CharacterStatusContainer? statusContainer;

#if DEBUG
    public bool GodMode; //for safety's sake, god mode isn't available outside of debug builds
#endif

    public float GetTiming(TimingStat type) => timingData[(int)type];
    public void SetTiming(TimingStat type, float val) => timingData[(int)type] = val;

    public bool IsSkillOnCooldown(CharacterSkill skill) => skillCooldowns.TryGetValue(skill, out var t) && t > Time.ElapsedTimeFloat;
    public void SetSkillCooldown(CharacterSkill skill, int val) => skillCooldowns[skill] = Time.ElapsedTimeFloat + val / 1000f;
    public void SetSkillCooldown(CharacterSkill skill, float val) => skillCooldowns[skill] = Time.ElapsedTimeFloat + val;

    public void ResetSkillCooldown(CharacterSkill skill) => skillCooldowns.Remove(skill);
    public void ResetSkillCooldowns() => skillCooldowns.Clear();

    public bool IsInSkillDamageCooldown(CharacterSkill skill) => damageCooldowns.TryGetValue(skill, out var t) && t > Time.ElapsedTimeFloat;
    public bool IsMagicImmune() => GetStat(CharacterStat.MagicImmunity) > 0;

    public void SetSkillDamageCooldown(CharacterSkill skill, float cooldown) => damageCooldowns[skill] = Time.ElapsedTimeFloat + cooldown;
    public void ResetSkillDamageCooldowns() => damageCooldowns.Clear();
    [ScriptUseable] public int HpPercent => GetStat(CharacterStat.Hp) * 100 / GetStat(CharacterStat.MaxHp);

    public float CastTimeRemaining => !IsCasting ? 0 : CastingTime - Time.ElapsedTimeFloat;

    public bool HasBodyState(BodyStateFlags flag) => (BodyState & flag) > 0;
    public void SetBodyState(BodyStateFlags flag) => BodyState |= flag;
    public void RemoveBodyState(BodyStateFlags flag) => BodyState &= ~flag;

    public void RefundSkillCooldownTime(CharacterSkill skill, float val)
    {
        if (skillCooldowns.TryGetValue(skill, out var t)) skillCooldowns[skill] -= val;
    }

    public void Reset()
    {
        Entity = Entity.Null;
        Character = null!;
        Player = null!;
        Faction = -1;
        Party = -1;
        BodyState = BodyStateFlags.None;
        IsTargetable = true;
        IsCasting = false;
        skillCooldowns.Clear();
        damageCooldowns.Clear();
        if (statusContainer != null)
        {
            statusContainer.ClearAllWithoutRemoveHandler();
            StatusEffectPoolManager.ReturnStatusContainer(statusContainer);
            statusContainer = null;
        }

#if DEBUG
        GodMode = false;
#endif

        //Array.Copy(statResetData, statData, statData.Length);

        for (var i = 0; i < statData.Length; i++)
            statData[i] = 0;
    }

    [ScriptUseable]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStat(CharacterStat type)
    {
        if (type < CharacterStat.MonsterStatsMax)
            return statData[(int)type];
        if (Entity.Type != EntityType.Player)
            return 0;
        return Player.PlayerStatData.TryGetValue(type, out var stat) ? stat : 0;
    }

    [ScriptUseable]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetStat(CharacterStat type, int val)
    {
        if (type < CharacterStat.MonsterStatsMax)
        {
            statData[(int)type] = val;
            return;
        }

        if (Entity.Type != EntityType.Player)
            return;
        Player.PlayerStatData[type] = val;
    }

    [ScriptUseable]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddStat(CharacterStat type, int val)
    {
        if (type < CharacterStat.MonsterStatsMax)
        {
            statData[(int)type] += val;
            return;
        }

        if (Entity.Type != EntityType.Player)
            return;

        ref var obj = ref CollectionsMarshal.GetValueRefOrNullRef(Player.PlayerStatData, type);
        if (Unsafe.IsNullRef(ref obj))
        {
            Player.PlayerStatData[type] = val;
            return;
        }

        obj += val;
    }

    [ScriptUseable]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubStat(CharacterStat type, int val) => AddStat(type, -val);

    [ScriptUseable]
    public void CreateEvent(string eventName, int param1 = 0, int param2 = 0, int param3 = 0, int param4 = 0)
    {
        if (Character.Map == null)
            return;
        World.Instance.CreateEvent(Entity, Character.Map, eventName, Character.Position, param1, param2, param3, param4, null);
    }

    [ScriptUseable]
    public bool IsEventNearby(string eventName, int range)
    {
        return Character.Map?.HasEventInArea(Area.CreateAroundPoint(Character.Position, range), eventName) ?? false;
    }

    [ScriptUseable]
    public void AddStatusEffect(CharacterStatusEffect effect, int duration, int val1 = 0, int val2 = 0)
    {
        var status = StatusEffectState.NewStatusEffect(effect, duration / 1000f, val1, val2);
        AddStatusEffect(status);
    }

    public void AddStatusEffect(StatusEffectState state, bool replaceExisting = true, float delay = 0)
    {
        //if (!Character.IsActive)
        //    return;
        if (Character.State == CharacterState.Dead)
            return; //cannot status a dead target

        if (statusContainer == null)
        {
            statusContainer = StatusEffectPoolManager.BorrowStatusContainer();
            statusContainer.Owner = this;
        }

        if (delay <= 0)
            statusContainer.AddNewStatusEffect(state, replaceExisting);
        else
            statusContainer.AddPendingStatusEffect(state, replaceExisting, delay);
    }
    
    [ScriptUseable]
    public bool RemoveStatusOfTypeIfExists(CharacterStatusEffect type)
    {
        if (statusContainer == null || statusContainer.TotalStatusEffectCount == 0) //that last one fixes an unfortunate issue where attempting to remove an existing status while adding a status breaks things
            return false;
        var success = statusContainer.RemoveStatusEffectOfType(type);
        if (!statusContainer.HasStatusEffects())
        {
            StatusEffectPoolManager.ReturnStatusContainer(statusContainer);
            statusContainer = null;
        }

        return success;
    }

    public StatusEffectState AddOrStackStatusEffect(CharacterStatusEffect type, float duration, int maxStacks = 16)
    {
        if (statusContainer == null)
        {
            statusContainer = StatusEffectPoolManager.BorrowStatusContainer();
            statusContainer.Owner = this;
        }

        if (statusContainer.TryGetExistingStatus(type, out var status))
        {
            if(status.Value4 < maxStacks)
                status.Value4++;
            status.Expiration = Time.ElapsedTime + duration;
        }
        else
            status = StatusEffectState.NewStatusEffect(type, duration, 0, 0, 0, 1);

        statusContainer.AddNewStatusEffect(status);

        return status;
    }

    public void RemoveStatusOfGroupIfExists(string groupName)
    {
        if (statusContainer == null || statusContainer.TotalStatusEffectCount == 0) //that last one fixes an unfortunate issue where attempting to remove an existing status while adding a status breaks things
            return;
        statusContainer.RemoveStatusEffectOfGroup(groupName);
        if (!statusContainer.HasStatusEffects())
        {
            StatusEffectPoolManager.ReturnStatusContainer(statusContainer);
            statusContainer = null;
        }
    }

    [ScriptUseable]
    public void ExpireStatusOfTypeIfExists(CharacterStatusEffect type)
    {
        if (statusContainer == null)
            return;
        statusContainer.ExpireStatusEffectOfType(type);
    }

    public void OnDeathClearStatusEffects(bool doRemoveUpdate = true)
    {
        if (statusContainer == null)
            return;
        if (doRemoveUpdate)
            statusContainer.RemoveAll();
        else
            statusContainer.ClearAllWithoutRemoveHandler();

        if (!statusContainer.HasStatusEffects() || !doRemoveUpdate)
        {
            StatusEffectPoolManager.ReturnStatusContainer(statusContainer);
            statusContainer = null;
        }
    }

    public void TryDeserializeStatusContainer(IBinaryMessageReader br, int saveVersion)
    {
        var count = (int)br.ReadByte();
        
        //Because status effects are serialized by id, we'll have a problem if the status effect ids ever change.
        //If the number of server status effects changes then, the player's status effect state is discarded.
        //We still need to deserialize though, as there's data after this we still need to load, but we won't use it.
        //At some point changing it to serializing by status name would prevent this limitation.

        var discardState = false;
        if (saveVersion >= 6) 
        {
            var totalEffects = (int)br.ReadInt16();
            if (totalEffects > (int)CharacterStatusEffect.StatusEffectMax)
                discardState = true;
        }

        if (count == 0)
            return;

        statusContainer = StatusEffectPoolManager.BorrowStatusContainer();
        statusContainer.Owner = this;
        
        if (!discardState)
            statusContainer.Deserialize(br, count);
        else
            statusContainer.DeserializeWithoutSave(br, count);
    }

    public bool CanPerformIndirectActions()
    {
        return Character.State != CharacterState.Dead && GetStat(CharacterStat.Disabled) == 0;
    }

    [ScriptUseable]
    public bool CanTeleport() => Character.Type == CharacterType.Player ? Character.Map?.CanTeleport ?? false : Character.Map?.CanMonstersTeleport ?? false;
    [ScriptUseable]
    public bool CanTeleportWithError()
    {
        if (!CanTeleport())
        {
            if (Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(Character.Player, SkillValidationResult.CannotTeleportHere);
            return false;
        }

        return true;
    }

    public bool IsFlying()
    {
        switch (Character.Type)
        {
            case CharacterType.Monster:
                return (Character.Monster.MonsterBase.SpecialFlags & MonsterSpecialFlags.Flying) > 0;
        }

        return false;
    }

    [ScriptUseable]
    public void RandomTeleport()
    {
        if (Character.Map == null)
            return;

        var pos = Character.Map.FindRandomPositionOnMap();

        if (Character.Type == CharacterType.Player)
            Player.AddInputActionDelay(InputActionCooldownType.Teleport);
        if (Character.Type == CharacterType.Monster)
            Character.Monster.AddDelay(1f);

        RemoveStatusOfGroupIfExists("StopGroup");

        ClearDamageQueue();
        Character.ResetState();
        if(Character.Type == CharacterType.Player)
            Character.SetSpawnImmunity();
        Character.Map.TeleportEntity(ref Entity, Character, pos);
        if (Character.Type == CharacterType.Player)
            CommandBuilder.SendExpGain(Player, 0); //update their exp

        if (Character.Type == CharacterType.Monster) //are they a monster? If so, we should check if they have minions that should come along.
        {
            var m = Character.Monster;
            if (m.Children == null)
                return;

            var area = Area.CreateAroundPoint(Character.Position, 2);

            for (var i = 0; i < m.ChildCount; i++)
            {
                var minion = m.Children[i];
                if (!minion.TryGet<WorldObject>(out var minionCharacter) || Character.Map != minionCharacter.Map)
                    continue;

                minionCharacter.StopMovingImmediately();
                Character.Map.TeleportEntity(ref minion, minionCharacter, Character.Map.GetRandomWalkablePositionInArea(area));
            }
        }
    }

    public CharacterStatusContainer? StatusContainer => statusContainer;
    public bool HasStatusEffectOfType(CharacterStatusEffect type) => statusContainer?.HasStatusEffectOfType(type) ?? false;

    public bool TryGetStatusContainer([NotNullWhen(returnValue: true)] out CharacterStatusContainer? status)
    {
        status = null;
        if (statusContainer == null)
            return false;
        status = statusContainer;
        return true;
    }

    public bool TryGetStatusEffect(CharacterStatusEffect effect, [NotNullWhen(returnValue: true)] out StatusEffectState status)
    {
        if (TryGetStatusContainer(out var container) && container.TryGetExistingStatus(effect, out status))
            return true;

        status = default;
        return false;
    }

    public void AddDisabledState()
    {
        if (Character.State == CharacterState.Dead)
            return;

        if (IsCasting)
        {
            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.StopCastMulti(Character);
            CommandBuilder.ClearRecipients();
            IsCasting = false;
        }

        Character.QueuedAction = QueuedAction.None;
        if (Character.IsMoving)
            Character.StopMovingImmediately();

        if (Character.Type == CharacterType.Player)
            Player.ClearTarget();

        AddStat(CharacterStat.Disabled, 1);
    }

    public void SubDisabledState()
    {
#if DEBUG
        if (GetStat(CharacterStat.Disabled) <= 0)
            ServerLogger.LogWarning($"Trying to remove disabled state while target is not disabled!");
#endif

        SubStat(CharacterStat.Disabled, 1);

        if (Character.Type == CharacterType.Monster && GetStat(CharacterStat.Disabled) <= 0)
        {
            Character.Monster.ResetAiUpdateTime();
            Character.Monster.ResetAiSkillUpdateTime();
        }
    }

    [ScriptUseable]
    public int GetEffectiveStat(CharacterStat type)
    {
        var stat = GetStat(type);

        switch (type)
        {
            case CharacterStat.Str:
                stat += GetStat(CharacterStat.AddStr);
                break;
            case CharacterStat.Agi:
                stat += GetStat(CharacterStat.AddAgi);
                break;
            case CharacterStat.Vit:
                stat += GetStat(CharacterStat.AddVit);
                break;
            case CharacterStat.Dex:
                stat += GetStat(CharacterStat.AddDex);
                break;
            case CharacterStat.Int:
                stat += GetStat(CharacterStat.AddInt);
                break;
            case CharacterStat.Luk:
                if (HasBodyState(BodyStateFlags.Curse))
                    return 0;
                stat += GetStat(CharacterStat.AddLuk);
                break;
            case CharacterStat.Def:
                stat += GetStat(CharacterStat.AddDef);
                if (Character.Type == CharacterType.Player)
                    stat += GetStat(CharacterStat.EquipmentRefineDef);
                stat = stat * (100 + GetStat(CharacterStat.AddDefPercent)) / 100;
                break;
            case CharacterStat.MDef:
                stat = (int)((stat + GetStat(CharacterStat.AddMDef)) * (1 + GetStat(CharacterStat.AddMDef) / 100f));
                break;
            case CharacterStat.PerfectDodge:
                stat += 1 + GetEffectiveStat(CharacterStat.Luck) / 10;
                break;
            case CharacterStat.Range:
                if (HasBodyState(BodyStateFlags.Blind) && stat > ServerConfig.MaxAttackRangeWhileBlind)
                    stat = ServerConfig.MaxAttackRangeWhileBlind;
                break;
            case CharacterStat.MoveSpeedBonus:
                if (stat < -99)
                    stat = -99; //at -100% move speed you'll probably have a divide by zero problem
                break;
        }

        return stat;
    }

    [ScriptUseable]
    public void UpdateStats()
    {
        if (Character.Type == CharacterType.Monster)
            Character.Monster.UpdateStats();
        if (Character.Type == CharacterType.Player)
            Character.Player.UpdateStats();
    }

    [ScriptUseable]
    public void HealHpPercent(int percent)
    {
        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);
        var hp = maxHp * percent / 100;

        if (curHp + hp > maxHp)
            hp = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + hp);

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendHealMulti(Character, hp, HealType.Item);
        CommandBuilder.ClearRecipients();
    }

    [ScriptUseable]
    public void HealSpPercent(int percent)
    {
        var curSp = GetStat(CharacterStat.Sp);
        var maxSp = GetStat(CharacterStat.MaxSp);
        var sp = maxSp * percent / 100;

        if (curSp + sp > maxSp)
            sp = maxSp - curSp;

        SetStat(CharacterStat.Sp, curSp + sp);

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.ChangeSpValue(Player, curSp + sp, maxSp);
        CommandBuilder.ClearRecipients();
    }

    public void HealHp(int hp, bool sendPacket = false, HealType type = HealType.None)
    {
        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);
        var realAmnt = hp;

        if (curHp + hp > maxHp)
            realAmnt = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + realAmnt);

        if (sendPacket)
        {
            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.SendHealMulti(Character, hp, type);
            CommandBuilder.ClearRecipients();
        }
    }

    [ScriptUseable]
    public void HealRange(int hp, int hp2, bool showValue = false)
    {
        if (hp2 != -1 && hp2 > hp)
            hp = GameRandom.NextInclusive(hp, hp2);

        if (Character.Type == CharacterType.Player)
            hp += hp * 10 * Character.Player.MaxLearnedLevelOfSkill(CharacterSkill.IncreasedHPRecovery) / 100;

        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);
        var chVit = GetEffectiveStat(CharacterStat.Vit);
        hp += hp * chVit / 50;

        if (curHp + hp > maxHp)
            hp = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + hp);

        if (Character.Map == null)
            return;

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendHealMulti(Character, hp, HealType.Item);
        CommandBuilder.ClearRecipients();
    }

    [ScriptUseable]
    public void RecoverSp(int sp, bool showValue = false) => RecoverSpRange(sp, -1, showValue);

    [ScriptUseable]
    public void RecoverSpRange(int sp, int sp2, bool showValue = false)
    {
        if (sp2 != -1 && sp2 > sp)
            sp = GameRandom.NextInclusive(sp, sp2);

        if (sp <= 0)
            return;

        if (Character.Type == CharacterType.Player)
            sp += sp * 10 * Character.Player.MaxLearnedLevelOfSkill(CharacterSkill.IncreaseSPRecovery) / 100;

        var curSp = GetStat(CharacterStat.Sp);
        var maxSp = GetStat(CharacterStat.MaxSp);
        var chInt = GetEffectiveStat(CharacterStat.Int);
        sp += sp * chInt / 50;

        if (curSp + sp > maxSp)
            sp = maxSp - curSp;

        SetStat(CharacterStat.Sp, curSp + sp);

        if (Character.Map == null || Character.Type != CharacterType.Player)
            return;

        CommandBuilder.ChangeSpValue(Player, curSp + sp, maxSp);
        if (Player.Party != null)
            CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(Player);
    }

    public void RecoverSpFixed(int sp)
    {
        if (sp <= 0)
            return;

        var curSp = GetStat(CharacterStat.Sp);
        var maxSp = GetStat(CharacterStat.MaxSp);
        var newSp = curSp + sp;

        if (newSp > maxSp)
            newSp = maxSp;

        SetStat(CharacterStat.Sp, newSp);

        if (Character.Map == null || Character.Type != CharacterType.Player)
            return;

        CommandBuilder.ChangeSpValue(Player, newSp, maxSp);
        if (Player.Party != null)
            CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(Player);
    }

    [ScriptUseable]
    public void FullRecovery(bool hp = true, bool sp = false)
    {
        if (hp)
            SetStat(CharacterStat.Hp, GetStat(CharacterStat.MaxHp));
        if (sp)
            SetStat(CharacterStat.Sp, GetStat(CharacterStat.MaxSp));
    }

    public bool IsValidAlly(CombatEntity source, bool canTargetHidden = false)
    {
        if (this == source)
            return false;
        if (Entity.IsNull() || !Entity.IsAlive())
            return false;
        if (!Character.IsActive || Character.State == CharacterState.Dead)
            return false;
        if (source.Character.Map != Character.Map)
            return false;

        if (source.Entity.Type != Character.Entity.Type)
            return false;
        if (!canTargetHidden && IsHiddenTo(source))
            return false;

        if (Character.ClassId >= 1000 && Character.ClassId < 4000)
            return false; //hack

        return true;
    }

    public bool IsHiddenTo(CombatEntity? source)
    {
        if (source != null)
        {
            var race = source.GetRace();
            if (source.GetSpecialType() == CharacterSpecialType.Boss || race == CharacterRace.Demon || race == CharacterRace.Insect)
                return false;
        }

        return (BodyState & BodyStateFlags.AnyHiddenState) > 0;
    }

    //Same as IsValidTarget, but can't see cloaked targets
    public bool CanBeTargeted(CombatEntity? source, bool canHarmAllies = false, bool canTargetHidden = false)
    {
        return IsValidTarget(source, canHarmAllies, canTargetHidden) && (canTargetHidden || !IsHiddenTo(source));
    }

    public bool IsValidTarget(CombatEntity? source, bool canHarmAllies = false, bool canTargetHidden = false)
    {
        if (this == source)
            return false;
        if (Entity.IsNull() || !Entity.IsAlive())
            return false;
        if (!Character.IsActive || Character.State == CharacterState.Dead || !IsTargetable)
            return false;
        if (Character.Map == null)
            return false;
        if (Character.AdminHidden)
            return false;

        if (!canTargetHidden && HasBodyState(BodyStateFlags.Hidden))
        {
            if (source == null)
                return false;
            var race = source.GetRace();
            if (source.GetSpecialType() != CharacterSpecialType.Boss && race != CharacterRace.Demon && race != CharacterRace.Insect)
                return false;
        }

        if (Character.IsTargetImmune)
            return false;
        //if (source.Character.ClassId == Character.ClassId)
        //    return false;
        if (source != null)
        {
            if (source.Character.Map != Character.Map)
                return false;
            if (!canHarmAllies && source.Entity.Type == EntityType.Player && Character.Entity.Type == EntityType.Player)
                return false;
            if (!canHarmAllies && source.Entity.Type == EntityType.Monster && Character.Entity.Type == EntityType.Monster)
                return false;
        }

        //if (source.Entity.Type == EntityType.Player)
        //    return false;

        if (Character.ClassId >= 1000 && Character.ClassId < 4000)
            return false; //hack
        //if (source.Party == Party)
        //    return false;
        //if (source.Faction == Faction)
        //    return false;

        return true;
    }

    public bool CanAttackTarget(WorldObject target, int range = -1, bool ignoreLineOfSight = false)
    {
        if (range == -1)
            range = GetEffectiveStat(CharacterStat.Range);
        if (!target.CombatEntity.IsTargetable)
            return false;
        if (DistanceCache.IntDistance(Character.Position, target.Position) > range)
            return false;
        if (ignoreLineOfSight)
            return true;
        if (Character.Map == null || !Character.Map.WalkData.HasLineOfSight(Character.Position, target.Position))
            return false;
        return true;
    }

    public bool CanAttackTargetFromPosition(WorldObject target, Position position)
    {
        if (!target.CombatEntity.IsTargetable)
            return false;
        if (DistanceCache.IntDistance(position, target.Position) > GetEffectiveStat(CharacterStat.Range))
            return false;
        if (Character.Map == null || !Character.Map.WalkData.HasLineOfSight(position, target.Position))
            return false;
        return true;
    }

    public void QueueDamage(DamageInfo info)
    {
        DamageQueue.Add(info);
        if (DamageQueue.Count > 1)
            DamageQueue.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    public bool HasFatalDamageQueued()
    {
        if (Character.Type != CharacterType.Monster || DamageQueue.Count == 0)
            return false;
        var hp = GetStat(CharacterStat.Hp);
        foreach (var d in DamageQueue)
            hp -= d.Damage;
        return hp < 0;
    }

    public void ClearDamageQueue()
    {
        DamageQueue.Clear();
    }

    private void FinishCasting()
    {
        IsCasting = false;

        var hasSpCost = Character.Type == CharacterType.Player && CastingSkill.ItemSource <= 0;
        if (hasSpCost && !Player.HasSpForSkill(CastingSkill.Skill, CastingSkill.Level))
        {
            CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
            return;
        }

        var spCost = hasSpCost ? Player.GetSpCostForSkill(CastingSkill.Skill, CastingSkill.Level) : 0;

        var validationResult = SkillHandler.ValidateTarget(CastingSkill, this, CastingSkill.IsIndirect, CastingSkill.ItemSource > 0);
        if (validationResult != SkillValidationResult.Success)
        {
            if (Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(Player, validationResult);
            return;
        }

        if (CastingSkill.ItemSource > 0 && Character.Type == CharacterType.Player)
        {
            if (Player.TryRemoveItemFromInventory(CastingSkill.ItemSource, 1))
                CommandBuilder.RemoveItemFromInventory(Player, CastingSkill.ItemSource, 1);
            else
            {
                CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientItemCount);
                return;
            }
        }

        if (SkillHandler.ExecuteSkill(CastingSkill, this))
        {
            if (hasSpCost)
                Player.TakeSpValue(spCost);
            if (Character.Type == CharacterType.Monster)
                Character.Monster.RunCastSuccessEvent();
        }
        else
            CommandBuilder.StopCastMultiAutoVis(Character);
    }

    public void ResumeQueuedSkillAction()
    {
        Character.QueuedAction = QueuedAction.None;

        if (QueuedCastingSkill.Level <= 0)
            return;

        if (QueuedCastingSkill.TargetedPosition != Position.Invalid)
        {
            AttemptStartGroundTargetedSkill(QueuedCastingSkill.TargetedPosition, QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime, QueuedCastingSkill.Flags);
            return;
        }

        var target = QueuedCastingSkill.TargetEntity.GetIfAlive<CombatEntity>();
        if (target == null) return;

        if (target == this)
        {
            AttemptStartSelfTargetSkill(QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime, QueuedCastingSkill.Flags);
            return;
        }

        AttemptStartSingleTargetSkillAttack(target, QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime, QueuedCastingSkill.Flags, QueuedCastingSkill.ItemSource);
    }

    public void QueueCast(SkillCastInfo skillInfo)
    {
        QueuedCastingSkill = skillInfo;
        Character.QueuedAction = QueuedAction.Cast;
    }

    public void UpdateHidingStateAfterAttack()
    {
        switch (Character.Type)
        {
            case CharacterType.Monster:
                if ((BodyState & BodyStateFlags.Cloaking) > 0 && GetSpecialType() != CharacterSpecialType.Boss)
                    RemoveStatusOfTypeIfExists(CharacterStatusEffect.Cloaking);
                return;
            case CharacterType.Player:
                if ((BodyState & BodyStateFlags.Cloaking) > 0)
                {
                    //RemoveStatusOfTypeIfExists(CharacterStatusEffect.Hiding);
                    RemoveStatusOfTypeIfExists(CharacterStatusEffect.Cloaking);
                }

                return;
        }
    }

    public void ModifyExistingCastTime(float modifier)
    {
        if (!IsCasting)
            return;
        var adjustedTime = CastTimeRemaining * (modifier - 1);
        CastingTime += adjustedTime;

        CommandBuilder.UpdateExistingCastMultiAutoVis(Character, adjustedTime);
    }

    public bool AttemptStartGroundTargetedSkill(Position target, CharacterSkill skill, int level, float castTime = -1, SkillCastFlags flags = SkillCastFlags.None)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError($"Cannot attempt a skill action {skill} while dead! " + Environment.StackTrace);
            return false;
        }

        if (Character.State == CharacterState.Sitting)
            return false;

        var skillInfo = new SkillCastInfo()
        {
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = target,
            Range = (sbyte)SkillHandler.GetSkillRange(this, skill, level),
            IsIndirect = false,
            Flags = flags
        };

        if (IsCasting) //if we're already casting, queue up the next cast
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.State == CharacterState.Moving) //if we are already moving, queue the skill action
        {
            if (Character.Type == CharacterType.Player)
                Player.ClearTarget(); //if we are currently moving we should dequeue attacking so we don't chase after casting

            Character.StopMovingImmediately();
            //QueueCast(skillInfo);

            //return true;
        }

        if (Character.Type == CharacterType.Player && Character.Position.DistanceTo(target) > skillInfo.Range) //if we are out of range, try to move closer
        {
            Player.ClearTarget();
            if (Character.InMoveLock && Character.MoveLockTime > Time.ElapsedTimeFloat) //we need to queue a cast so we both move and cast once the lock ends
            {
                QueueCast(skillInfo);
                return true;
            }

            if (Character.TryMove(target, 1))
            {
                QueueCast(skillInfo);
                return true;
            }

            return false;
        }

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.Type == CharacterType.Player)
        {
            Player.NotifyOfSkillCastAttempt(skill);

            if (!Player.HasSpForSkill(skill, level))
            {
                CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
                return false;
            }

            if (Player.SkillCooldownTime > Time.ElapsedTimeFloat)
            {
                QueueCast(skillInfo);
                return true;
            }
        }

        CastingSkill = skillInfo;
        if (target.IsValid() && target != Character.Position)
            Character.FacingDirection = DistanceCache.Direction(Character.Position, target);

        if (castTime < 0f)
            castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, null, skillInfo.Level);
        if (castTime <= 0)
            ExecuteQueuedSkillAttack();
        else
        {
            if (Character.Type == CharacterType.Player) //monsters have their interrupt mode set during their AI skill handler
            {
                var skillData = DataManager.SkillData[skill];
                CastInterruptionMode = skillData.InterruptMode == CastInterruptionMode.Default ? ServerConfig.OperationConfig.DefaultCastInterruptMode : skillData.InterruptMode;

                castTime = castTime * (100 + GetStat(CharacterStat.AddCastTime)) / 100;
            }

            IsCasting = true;
            CastingTime = Time.ElapsedTimeFloat + castTime;
            ApplyCooldownForAttackAction();

            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.StartCastGroundTargetedMulti(Character, target, skillInfo.Skill, skillInfo.Level, skillInfo.Range, castTime, flags);
            CommandBuilder.ClearRecipients();
        }
        return true;
    }

    public bool AttemptStartSelfTargetSkill(CharacterSkill skill, int level, float castTime = -1f, SkillCastFlags flags = SkillCastFlags.None)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError($"Cannot attempt a skill action {skill} while dead! " + Environment.StackTrace);
            return false;
        }

        if (Character.Type == CharacterType.Player)
        {
            if (Character.State == CharacterState.Sitting)
                return false;

            if (level <= 0)
                level = Player.MaxLearnedLevelOfSkill(skill);
        }

        var skillInfo = new SkillCastInfo()
        {
            TargetEntity = Entity,
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = Position.Invalid,
            IsIndirect = false,
            Flags = flags
        };

        if (IsCasting) //if we're already casting, queue up the next cast
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.State == CharacterState.Moving) //if we are already moving, queue the skill action
        {
            if (Character.Type == CharacterType.Player)
                Character.Player.ClearTarget(); //if we are currently moving we should dequeue attacking so we don't chase after casting

            if (skill != CharacterSkill.NoCast || castTime > 0)
                Character.StopMovingImmediately();
            //Character.ShortenMovePath(); //for some reason shorten move path cancels the queued action and I'm too lazy to find out why
            //QueueCast(skillInfo);

            //return true;
        }

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.Type == CharacterType.Player)
        {
            Player.NotifyOfSkillCastAttempt(skill);

            if (!Player.HasSpForSkill(skill, level))
            {
                CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
                return false;
            }

            if (Player.SkillCooldownTime > Time.ElapsedTimeFloat)
            {
                QueueCast(skillInfo);
                return true;
            }
        }

        CastingSkill = skillInfo;

        if (castTime < 0f)
            castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, null, skillInfo.Level);
        if (castTime <= 0)
            ExecuteQueuedSkillAttack();
        else
        {
            if (Character.Type == CharacterType.Player) //monsters have their interrupt mode set during their AI skill handler
            {
                var skillData = DataManager.SkillData[skill];
                CastInterruptionMode = skillData.InterruptMode == CastInterruptionMode.Default ? ServerConfig.OperationConfig.DefaultCastInterruptMode : skillData.InterruptMode;
                castTime = castTime * (100 + GetStat(CharacterStat.AddCastTime)) / 100;
            }

            IsCasting = true;
            CastingTime = Time.ElapsedTimeFloat + castTime;
            ApplyCooldownForAttackAction();

            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            var clientSkill = skillInfo.Skill;
            CommandBuilder.StartCastMulti(Character, null, clientSkill, skillInfo.Level, castTime, flags);
            CommandBuilder.ClearRecipients();
        }
        return true;
    }

    public bool AttemptStartSingleTargetSkillAttack(CombatEntity target, CharacterSkill skill, int level, float castTime = -1f, SkillCastFlags flags = SkillCastFlags.None, int itemSrc = -1)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Sitting)
            return false;

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError("Cannot attempt a skill action while dead! " + Environment.StackTrace);
            return false;
        }

        if (Character.Type == CharacterType.Player && itemSrc <= 0 && !Character.Player.VerifyCanUseSkill(skill, level))
        {
            ServerLogger.Log($"Player {Character.Name} attempted to use skill {skill} lvl {level}, but they cannot use that skill.");
            Character.QueuedAction = QueuedAction.None;
            return false;
        }

        var skillInfo = new SkillCastInfo()
        {
            TargetEntity = target.Entity,
            Skill = skill,
            Level = level,
            CastTime = castTime,
            Range = (sbyte)SkillHandler.GetSkillRange(this, skill, level),
            TargetedPosition = Position.Invalid,
            IsIndirect = false,
            ItemSource = (short)itemSrc,
            Flags = flags
        };

        if (IsCasting) //if we're already casting, queue up the next cast
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.State == CharacterState.Moving) //if we are already moving, queue the skill action
        {
            if (Character.Type == CharacterType.Player)
                Character.Player.ClearTarget(); //if we are currently moving we should dequeue attacking so we don't chase after casting

            Character.ShortenMovePath(); //for some reason shorten move path cancels the queued action and I'm too lazy to find out why
            QueueCast(skillInfo);

            return true;
        }

        //if we are out of range, try to move closer (but only players, monsters should be checking their range before this point)
        if (Character.Type == CharacterType.Player && Character.Position.DistanceTo(target.Character.Position) > skillInfo.Range)
        {
            Character.Player.ClearTarget();
            if (Character.InMoveLock && Character.MoveLockTime > Time.ElapsedTimeFloat) //we need to queue a cast so we both move and cast once the lock ends
            {
                QueueCast(skillInfo);
                return true;
            }

            if (Character.TryMove(target.Character.Position, 1))
            {
                QueueCast(skillInfo);
                return true;
            }

            return false;
        }

        if (Character.Type == CharacterType.Player)
        {
            Player.NotifyOfSkillCastAttempt(skill);

            if (itemSrc <= 0 && !Player.HasSpForSkill(skill, level))
            {
                CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
                return false;
            }

            if (Player.SkillCooldownTime > Time.ElapsedTimeFloat)
            {
                QueueCast(skillInfo);
                return true;
            }
        }

        var res = SkillHandler.ValidateTarget(skillInfo, this, false, itemSrc > 0);

        if (res == SkillValidationResult.Success)
        {
            CastingSkill = skillInfo;

            if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            {
                //var duration = Character.ActionDelay - Time.ElapsedTimeFloat;
                //ServerLogger.LogWarning($"Entity {Character.Name} currently in an action delay({duration} => {Character.ActionDelay}/{Time.ElapsedTimeFloat}), it's skill cast will be queued.");
                QueueCast(skillInfo);
                return true;
            }

            //don't turn character if you target yourself!
            if (Character.Position != target.Character.Position)
                Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);

            if (castTime < 0f)
                castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, target, skillInfo.Level);
            if (castTime <= 0)
                ExecuteQueuedSkillAttack();
            else
            {
                if (Character.Type == CharacterType.Player) //monsters have their interrupt mode set during their AI skill handler
                {
                    var skillData = DataManager.SkillData[skill];
                    CastInterruptionMode = skillData.InterruptMode == CastInterruptionMode.Default ? ServerConfig.OperationConfig.DefaultCastInterruptMode : skillData.InterruptMode;

                    castTime = castTime * (100 + GetStat(CharacterStat.AddCastTime)) / 100;
                }

                IsCasting = true;
                CastingTime = Time.ElapsedTimeFloat + castTime;
                ApplyCooldownForAttackAction();

                Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.StartCastMulti(Character, target.Character, skillInfo.Skill, skillInfo.Level, castTime, flags);
                CommandBuilder.ClearRecipients();

                if (target.Character.Type == CharacterType.Monster)
                    target.Character.Monster.MagicLock(this);
            }
            return true;
        }
        else
        {
            if (Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(Character.Player, res);
        }

        return false;
    }

    public void ExecuteQueuedSkillAttack()
    {
        //if (!QueuedCastingSkill.IsValid)
        //    throw new Exception($"We shouldn't be attempting to execute a queued action when no queued action exists!");
        //CastingSkill = QueuedCastingSkill;
        var res = SkillHandler.ValidateTarget(CastingSkill, this);
        if (res != SkillValidationResult.Success)
        {
            if (Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(Player, res);
#if DEBUG
            ServerLogger.Log($"Character {Character} failed a queued skill attack with the validation result: {res}");
#endif
            return;
        }

        var hasSpCost = Character.Type == CharacterType.Player && CastingSkill.ItemSource <= 0;

        if (hasSpCost && !Player.HasSpForSkill(CastingSkill.Skill, CastingSkill.Level))
        {
            CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
            return;
        }

        var spCost = hasSpCost ? Player.GetSpCostForSkill(CastingSkill.Skill, CastingSkill.Level) : 0;
        var hasExecuted = false;
        var hasImmunity = Character.IsTargetImmune;
        if (CastingSkill.ItemSource > 0 && Character.Type == CharacterType.Player)
        {
            if (Player.TryRemoveItemFromInventory(CastingSkill.ItemSource, 1))
            {
                CommandBuilder.RemoveItemFromInventory(Player, CastingSkill.ItemSource, 1);
                hasExecuted = SkillHandler.ExecuteSkill(CastingSkill, this);
            }
        }
        else
            hasExecuted = SkillHandler.ExecuteSkill(CastingSkill, this);

        if (hasSpCost && hasExecuted)
            Player.TakeSpValue(spCost);
        else
            CommandBuilder.StopCastMultiAutoVis(Character);

        CastingSkill.Clear();
        QueuedCastingSkill.Clear();
        if(hasImmunity && CastingSkill.Skill != CharacterSkill.Teleport)
            Character.ResetSpawnImmunity();
    }

    public CharacterRace GetRace()
    {
        return Character.Type == CharacterType.Monster ? Character.Monster.MonsterBase.Race : CharacterRace.Demihuman;
    }

    public CharacterElement GetElement()
    {
        var element = CharacterElement.Neutral1;
        if (Character.Type == CharacterType.Monster)
            element = Character.Monster.MonsterBase.Element;
        if (Character.Type == CharacterType.Player)
            element = Player.Equipment.ArmorElement;

        var overrideElement = (CharacterElement)GetStat(CharacterStat.OverrideElement);
        if (overrideElement != 0 && overrideElement != CharacterElement.None)
            element = overrideElement;
        return element;
    }

    public AttackElement GetAttackTypeForDefenderElement(CharacterElement element)
    {
        if (element >= CharacterElement.None)
            return AttackElement.Neutral;
        return (AttackElement)((int)element / 4);
    }

    public bool IsElementBaseType(CharacterElement targetType)
    {
        var element = GetElement();
        return element >= targetType && element <= targetType + 3; //checks if element 1-4 is equal to targetType (assuming we pass element 1)
    }

    public CharacterSpecialType GetSpecialType()
    {
        if (Character.Type != CharacterType.Monster)
            return CharacterSpecialType.Normal;
        return Character.Monster.MonsterBase.Special;
    }

    public void ExecuteIndirectSkillAttack(SkillCastInfo info)
    {
        SkillHandler.ExecuteSkill(info, this);
    }

    /// <summary>
    /// Scales a chance by the luck difference between targets using a sliding modifier value.
    /// On most rolls a luck difference of 100 will double or halve your chance of success.
    /// On rolls higher than 10% the benefit luck provides decreases.
    /// (at 100 luck difference, a roll of 10% chance increases to 20%, but a roll of 50% it only boosts it to 75%)
    /// </summary>
    /// <param name="target">The defender of the attack</param>
    /// <param name="chance">The highest random value that counts as a success</param>
    /// <param name="outOf">The maximum value used when rolling on a chance of success</param>
    /// <returns></returns>
    public bool CheckLuckModifiedRandomChanceVsTarget(CombatEntity target, int chance, int outOf)
    {
        if (chance == 0)
            return false;

        if (chance > outOf)
            return true;

        var luckMod = 100;
        var successRate = chance / (float)outOf;
        if (successRate > 0.1f)
            luckMod += (int)((successRate * 1000 - 100) / 4f);

        var attackerLuck = GetEffectiveStat(CharacterStat.Luck);
        var targetLuck = target.GetEffectiveStat(CharacterStat.Luck);

        //cap monster luck to only ever exceed player luck by 100. Jokers were having too good of a time.
        if (Character.Type == CharacterType.Monster && target.Character.Type == CharacterType.Player && attackerLuck > targetLuck + 100)
            attackerLuck = targetLuck + 100;
        if (Character.Type == CharacterType.Player && target.Character.Type == CharacterType.Monster && attackerLuck + 100 < targetLuck)
            targetLuck = attackerLuck + 100;


        if (targetLuck < 0 || target == this) targetLuck = 0; //note: self targeted chances count as though the target has 0 luck

        var realChance = chance * 10 * (attackerLuck + luckMod) / (targetLuck + luckMod);
        if (realChance <= 0)
            return false;

        return GameRandom.NextInclusive(0, outOf * 10) < realChance;
    }

    /// <summary>
    /// Test to see if this character is able to hit the enemy.
    /// </summary>
    /// <returns>Returns true if the attack hits</returns>
    public bool TestHitVsEvasion(CombatEntity target, int attackerHitBonus = 100, int flatEvasionPenalty = 0)
    {
        var attackerHit = GetStat(CharacterStat.Level) + GetEffectiveStat(CharacterStat.Dex) + GetStat(CharacterStat.AddHit);

        var defenderAgi = target.GetEffectiveStat(CharacterStat.Agi);
        var defenderFlee = target.GetStat(CharacterStat.Level) + defenderAgi + target.GetStat(CharacterStat.AddFlee);

        if (defenderFlee < 0)
            return true;

        var hitSuccessRate = attackerHit + 75 - defenderFlee;

        if (hitSuccessRate < 5) hitSuccessRate = 5;
        //if (hitSuccessRate > 95 && target.Character.Type == CharacterType.Player) hitSuccessRate = 95;

        hitSuccessRate = hitSuccessRate * attackerHitBonus / 100;

        if (flatEvasionPenalty > 0)
            hitSuccessRate = int.Clamp(hitSuccessRate + flatEvasionPenalty, 5, 100);

        return hitSuccessRate > GameRandom.Next(0, 100);
    }

    public bool TestHitVsEvasionWithAttackerPenalty(CombatEntity target) => TestHitVsEvasion(target, 100, target.GetAttackerPenalty(Entity));

    public int GetAttackerPenalty(Entity attacker)
    {
        var attackerPenalty = 0; //number of attackers above 2 (player only consideration)
        if (Character.Type == CharacterType.Player)
        {
            Player.RegisterRecentAttacker(ref attacker);

            var attackerCount = Player.GetCurrentAttackerCount();
            attackerPenalty = attackerCount > 2 ? attackerCount - 2 : 0;
        }

        return attackerPenalty;
    }



    public DamageInfo PrepareTargetedSkillResult(CombatEntity? target, CharacterSkill skillSource = CharacterSkill.None)
    {
        //technically the motion time is how long it's locked in place, we use sprite timing if it's faster.
        var spriteTiming = GetTiming(TimingStat.SpriteAttackTiming);
        var delayTiming = GetTiming(TimingStat.AttackDelayTime);
        var motionTiming = GetTiming(TimingStat.AttackMotionTime);
        if (motionTiming > delayTiming)
            delayTiming = motionTiming;

        //delayTiming -= 0.2f;
        //if (delayTiming < 0.2f)
        //    delayTiming = 0.2f;

        if (spriteTiming > delayTiming)
            spriteTiming = delayTiming;

        var di = new DamageInfo()
        {
            KnockBack = 0,
            Source = Entity,
            Target = target?.Entity ?? Entity.Null,
            AttackSkill = skillSource,
            Time = Time.ElapsedTimeFloat + spriteTiming,
            AttackMotionTime = spriteTiming,
            AttackPosition = Character.Position,
            Flags = DamageApplicationFlags.None
        };

        return di;
    }

    public float GetAttackMotionTime()
    {
        var spriteTiming = GetTiming(TimingStat.SpriteAttackTiming);
        var delayTiming = GetTiming(TimingStat.AttackDelayTime);
        var motionTiming = GetTiming(TimingStat.AttackMotionTime);
        if (motionTiming > delayTiming)
            delayTiming = motionTiming;

        if (spriteTiming > delayTiming)
            spriteTiming = delayTiming;

        return spriteTiming;
    }

    public DamageInfo CalculateCombatResult(CombatEntity target, float attackMultiplier, int hitCount,
        AttackFlags flags, CharacterSkill skillSource = CharacterSkill.None, AttackElement element = AttackElement.None)
    {
        var req = new AttackRequest(skillSource, attackMultiplier, hitCount, flags, element);
        return CalculateCombatResult(target, req);
    }

    public DamageInfo CalculateCombatResult(CombatEntity target, AttackRequest req)
    {
#if DEBUG
        if (!target.IsValidTarget(this, req.Flags.HasFlag(AttackFlags.CanHarmAllies), true))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling CalculateCombatResult.");
#endif
        var isMagic = req.Flags.HasFlag(AttackFlags.Magical);
        if (req.MinAtk == 0 && req.MaxAtk == 0)
            (req.MinAtk, req.MaxAtk) = CalculateAttackPowerRange(isMagic);

        statusContainer?.OnPreCalculateCombatResult(target, ref req);

        return CalculateCombatResultUsingSetAttackPower(target, req);
    }

    public void ApplyCooldownForAttackAction(CombatEntity target)
    {
#if DEBUG
        if (!target.IsValidTarget(this, true))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling PerformAttackAction.");
#endif
        ApplyCooldownForAttackAction();

        if (Character.Position != target.Character.Position)
            Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);
    }

    public void ApplyCooldownForAttackAction(Position target)
    {
        ApplyCooldownForAttackAction();

        if (Character.Position != target)
            Character.FacingDirection = DistanceCache.Direction(Character.Position, target);
    }

    //support skills differ in that there's a max amount of delay regardless of aspd
    public void ApplyCooldownForSupportSkillAction(float maxCooldownTime = 1f)
    {
        if (Character.Type != CharacterType.Player)
        {
            ApplyCooldownForAttackAction(); //players override motion time on support skills, but monsters should not
            return;
        }

        var attackMotionTime = GetTiming(TimingStat.AttackMotionTime) / 2f; //time for actual weapon strike to occur
        var delayTime = GetTiming(TimingStat.AttackDelayTime); //time before you can attack again

        if (delayTime > maxCooldownTime)
            delayTime = maxCooldownTime;

        //if (attackMotionTime > delayTime)
        //    delayTime = attackMotionTime;

        //if (Character.AttackCooldown + Time.DeltaTime + 0.005d < Time.ElapsedTime)
        if(Time.ElapsedTime + delayTime > Character.AttackCooldown)
            Character.AttackCooldown = Time.ElapsedTime + delayTime; //they are consecutively attacking
        //else
        //    Character.AttackCooldown += delayTime;

        Character.AddMoveLockTime(attackMotionTime); //the actual animation is 6 frames instead of 9 for skill casting
    }

    public void ApplyCooldownForAttackAction(float maxMotionTime = 4f)
    {
        var attackMotionTime = GetTiming(TimingStat.AttackMotionTime); //time for actual weapon strike to occur
        var delayTime = GetTiming(TimingStat.AttackDelayTime); //time before you can attack again

        if (attackMotionTime > maxMotionTime)
            attackMotionTime = maxMotionTime;

        if (attackMotionTime > delayTime)
            delayTime = attackMotionTime;

        if (Character.AttackCooldown + Time.DeltaTime + 0.005d < Time.ElapsedTime)
            Character.AttackCooldown = Time.ElapsedTime + delayTime; //they are consecutively attacking
        else
            Character.AttackCooldown += delayTime;

        if (Character.Type == CharacterType.Monster)
            Character.Monster.AddDelay(attackMotionTime);

        Character.AddMoveLockTime(attackMotionTime);

        //if(Character.Type == CharacterType.Monster)
        //    Character.Monster.AddDelay(attackMotionTime);
    }

    /// <summary>
    /// Applies a damageInfo to the enemy combatant. Use sendPacket = false to suppress sending an attack packet if you plan to send a different packet later.
    /// </summary>
    /// <param name="damageInfo">Damage to apply, sourced from this combat entity.</param>
    /// <param name="sendPacket">Set to true if you wish to automatically send an Attack packet.</param>
    public void ExecuteCombatResult(DamageInfo damageInfo, bool sendPacket = true, bool showAttackMotion = true)
    {
        var target = damageInfo.Target.Get<CombatEntity>();

        if (sendPacket)
        {
            if (damageInfo.AttackSkill == CharacterSkill.None)
            {
                Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.AttackMulti(Character, target.Character, damageInfo, showAttackMotion);
                CommandBuilder.ClearRecipients();
            }
            else
            {
                Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.SkillExecuteTargetedSkill(Character, target.Character, damageInfo.AttackSkill, 1, damageInfo);
                CommandBuilder.ClearRecipients();
            }
        }

        if (damageInfo.Damage != 0)
            target.QueueDamage(damageInfo);

        
    }

    public void PerformMeleeAttack(CombatEntity target)
    {
        ApplyCooldownForAttackAction(target);

        var flags = AttackFlags.Physical;
        if (Character.Type == CharacterType.Player)
            flags |= AttackFlags.CanCrit;
        if (Character.Type == CharacterType.Monster)
            flags |= AttackFlags.CanHarmAllies; //they can auto attack their allies if they get hit by them

        var di = CalculateCombatResult(target, 1f, 1, flags);
        if (Character.Type == CharacterType.Player && (Character.Player.WeaponClass == 1 || Character.Player.Equipment.DoubleAttackModifiers > 0))
        {
            var doubleChance = GetStat(CharacterStat.DoubleAttackChance);
            if (doubleChance > 0 && GameRandom.Next(0, 100) <= doubleChance)
                di.HitCount = 2;
        }

        UpdateHidingStateAfterAttack();

        ExecuteCombatResult(di);
    }

    public void PerformMagicSkillMotion(float cooldown, ref DamageInfo res)
    {

    }

    public void ApplyAfterCastDelay(float time)
    {
        if (Character.Type == CharacterType.Player)
            Player.ApplyAfterCastDelay(time);
    }

    public void ApplyAfterCastDelay(float time, ref DamageInfo res)
    {
        if (Character.Type != CharacterType.Player)
            return;

        if (res.AttackMotionTime < time)
            res.AttackMotionTime = float.Min(time, 0.6f);

        ApplyAfterCastDelay(time);
    }

    public void CancelCast()
    {
        if (!IsCasting)
            return;
        if (CastInterruptionMode == CastInterruptionMode.NeverInterrupt)
            return;

        //monster skill cooldowns should start where a cast is cancelled, but since we set it to delay + cast time, we refund the extra time
        if (Character.Type == CharacterType.Monster && CastTimeRemaining > 0)
            RefundSkillCooldownTime(CastingSkill.Skill, CastTimeRemaining);

        Character.AttackCooldown = 0;
        Character.MoveLockTime = 0;

        IsCasting = false;
        if (!Character.HasVisiblePlayers())
            return;


        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.StopCastMulti(Character);
        CommandBuilder.ClearRecipients();

    }


    private void AttackUpdate()
    {
        var hasResult = false;
        while (DamageQueue.Count > 0 && DamageQueue[0].Time < Time.ElapsedTimeFloat)
        {
            var di = DamageQueue[0];
            DamageQueue.RemoveAt(0);

            ApplyQueuedCombatResult(ref di);
            if (di.Damage > 0)
                hasResult = true;
        }
        if (hasResult && Character.Type == CharacterType.Player && Player.Party != null)
            CommandBuilder.UpdatePartyMembersOnMapOfHpSpChange(Player, false); //notify party members out of sight

    }

    public void Init(ref Entity e, WorldObject ch)
    {
        Entity = e;
        Character = ch;
        IsTargetable = true;
        if (e.Type == EntityType.Player)
            Player = ch.Player;

        if (DamageQueue == null!)
            DamageQueue = new List<DamageInfo>(4);

        DamageQueue.Clear();

        SetStat(CharacterStat.Range, 2);
    }

    public void Update()
    {
        if (!Character.IsActive)
            return;

        var time = Time.ElapsedTimeFloat;

        if (IsCasting && CastingTime < time)
            FinishCasting();

        if (DamageQueue.Count > 0)
            AttackUpdate();

        if (Character.Type == CharacterType.Player && Player.IndirectCastQueue.Count > 0)
            Player.IndirectCastQueueUpdate(); //we handle this in combat entity to guarantee it happens after damage queue update

        if (statusContainer != null && Character.IsActive) //we could have gone inactive after attack update
            statusContainer.UpdateStatusEffects();
    }
}
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Net.Mime.MediaTypeNames;
using RoRebuildServer.Database.Domain;
using System.Xml.Linq;
using System.Threading;
using RebuildSharedData.ClientTypes;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(new[] { EntityType.Player, EntityType.Monster })]
public class CombatEntity : IEntityAutoReset
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


    public float GetTiming(TimingStat type) => timingData[(int)type];
    public void SetTiming(TimingStat type, float val) => timingData[(int)type] = val;

    public bool IsSkillOnCooldown(CharacterSkill skill) => skillCooldowns.TryGetValue(skill, out var t) && t > Time.ElapsedTimeFloat;
    public void SetSkillCooldown(CharacterSkill skill, float val) => skillCooldowns[skill] = Time.ElapsedTimeFloat + val;
    public void ResetSkillCooldowns() => skillCooldowns.Clear();

    public bool IsInSkillDamageCooldown(CharacterSkill skill) => damageCooldowns.TryGetValue(skill, out var t) && t >= Time.ElapsedTimeFloat;

    public void SetSkillDamageCooldown(CharacterSkill skill, float cooldown) => damageCooldowns[skill] = Time.ElapsedTimeFloat + cooldown;
    public void ResetSkillDamageCooldowns() => damageCooldowns.Clear();
    public int HpPercent => GetStat(CharacterStat.Hp) * 100 / GetStat(CharacterStat.MaxHp);

    public float CastTimeRemaining => !IsCasting ? 0 : CastingTime - Time.ElapsedTimeFloat;


    public void Reset()
    {
        Entity = Entity.Null;
        Character = null!;
        Player = null!;
        Faction = -1;
        Party = -1;
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

        //Array.Copy(statResetData, statData, statData.Length);

        for (var i = 0; i < statData.Length; i++)
            statData[i] = 0;
    }

    public int GetStat(CharacterStat type)
    {
        if (type < CharacterStat.MonsterStatsMax)
            return statData[(int)type];
        if (Entity.Type != EntityType.Player)
            return 0;
        return Player.PlayerStatData[type - CharacterStat.MonsterStatsMax];
    }

    public void SetStat(CharacterStat type, int val)
    {
        if (type < CharacterStat.MonsterStatsMax)
        {
            statData[(int)type] = val;
            return;
        }

        if (Entity.Type != EntityType.Player)
            return;
        Player.PlayerStatData[type - CharacterStat.MonsterStatsMax] = val;
    }

    public void AddStat(CharacterStat type, int val)
    {
        if (type < CharacterStat.MonsterStatsMax)
        {
            statData[(int)type] += val;
            return;
        }

        if (Entity.Type != EntityType.Player)
            return;
        Player.PlayerStatData[type - CharacterStat.MonsterStatsMax] += val;
    }

    public void SubStat(CharacterStat type, int val)
    {
        if (type < CharacterStat.MonsterStatsMax)
        {
            statData[(int)type] -= val;
            return;
        }

        if (Entity.Type != EntityType.Player)
            return;
        Player.PlayerStatData[type - CharacterStat.MonsterStatsMax] -= val;
    }

    public void AddStatusEffect(CharacterStatusEffect effect, float duration, int val1 = 0, int val2 = 0)
    {
        var status = StatusEffectState.NewStatusEffect(effect, duration, val1, val2);
        AddStatusEffect(status);
    }

    public void AddStatusEffect(StatusEffectState state, bool replaceExisting = true, float delay = 0)
    {
        if (!Character.IsActive)
            return;

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

    public void RemoveStatusOfTypeIfExists(CharacterStatusEffect type)
    {
        if (statusContainer == null)
            return;
        statusContainer.RemoveStatusEffectOfType(type);
        if (!statusContainer.HasStatusEffects())
        {
            StatusEffectPoolManager.ReturnStatusContainer(statusContainer);
            statusContainer = null;
        }
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

    public CharacterStatusContainer? GetStatusEffectData() => statusContainer;
    public bool HasStatusEffectOfType(CharacterStatusEffect type) => statusContainer?.HasStatusEffectOfType(type) ?? false;

    public bool TryGetStatusContainer([NotNullWhen(returnValue: true)] out CharacterStatusContainer? status)
    {
        status = null;
        if (statusContainer == null)
            return false;
        status = statusContainer;
        return true;
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
                if (BodyState.HasFlag(BodyStateFlags.Curse))
                    return 0;
                stat += GetStat(CharacterStat.AddLuk);
                break;
            case CharacterStat.Def:
                stat = (int)((stat + GetStat(CharacterStat.AddDef)) * (1 + GetStat(CharacterStat.AddDefPercent) / 100f));
                break;
            case CharacterStat.MDef:
                stat = (int)((stat + GetStat(CharacterStat.AddMDef)) * (1 + GetStat(CharacterStat.AddMDef) / 100f));
                break;
        }

        return stat;
    }

    public void UpdateStats()
    {
        if (Character.Type == CharacterType.Monster)
            Character.Monster.UpdateStats();
        if (Character.Type == CharacterType.Player)
            Character.Player.UpdateStats();
    }

    public void HealHp(int hp)
    {
        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);

        if (curHp + hp > maxHp)
            hp = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + hp);
    }

    public void HealRange(int hp, int hp2, bool showValue = false)
    {
        if (hp2 != -1 && hp2 > hp)
            hp = GameRandom.NextInclusive(hp, hp2);

        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);
        var chVit = GetEffectiveStat(CharacterStat.Vit);
        hp += hp * chVit / 100;

        if (curHp + hp > maxHp)
            hp = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + hp);

        if (Character.Map == null)
            return;

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendHealMulti(Character, hp, HealType.Item);
        CommandBuilder.ClearRecipients();
    }


    public void RecoverSpRange(int sp, int sp2, bool showValue = false)
    {
        if (sp2 != -1 && sp2 > sp)
            sp = GameRandom.NextInclusive(sp, sp2);

        var curSp = GetStat(CharacterStat.Sp);
        var maxSp = GetStat(CharacterStat.MaxSp);
        var chInt = GetEffectiveStat(CharacterStat.Int);
        sp += sp * chInt / 100;

        if (curSp + sp > maxSp)
            sp = maxSp - curSp;

        SetStat(CharacterStat.Sp, curSp + sp);

        if (Character.Map == null || Character.Type != CharacterType.Player)
            return;

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.ChangeSpValue(Player, curSp + sp, maxSp);
        CommandBuilder.ClearRecipients();
    }

    public void FullRecovery(bool hp = true, bool sp = false)
    {
        if (hp)
            SetStat(CharacterStat.Hp, GetStat(CharacterStat.MaxHp));
        if (sp)
            SetStat(CharacterStat.Sp, GetStat(CharacterStat.MaxSp));
    }

    public bool IsValidAlly(CombatEntity source)
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

        if (Character.ClassId >= 1000 && Character.ClassId < 4000)
            return false; //hack

        return true;
    }

    public bool IsValidTarget(CombatEntity? source, bool canHarmAllies = false)
    {
        if (this == source)
            return false;
        if (Entity.IsNull() || !Entity.IsAlive())
            return false;
        if (!Character.IsActive || Character.State == CharacterState.Dead || !IsTargetable)
            return false;
        if (Character.Map == null)
            return false;
        if (Character.Hidden)
            return false;

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

    public bool CanAttackTarget(WorldObject target, int range = -1)
    {
        if (range == -1)
            range = GetStat(CharacterStat.Range);
        if (!target.CombatEntity.IsTargetable)
            return false;
        if (DistanceCache.IntDistance(Character.Position, target.Position) > range)
            return false;
        if (Character.Map == null || !Character.Map.WalkData.HasLineOfSight(Character.Position, target.Position))
            return false;
        return true;
    }

    public bool CanAttackTargetFromPosition(WorldObject target, Position position)
    {
        if (!target.CombatEntity.IsTargetable)
            return false;
        if (DistanceCache.IntDistance(position, target.Position) > GetStat(CharacterStat.Range))
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

    public void ClearDamageQueue()
    {
        DamageQueue.Clear();
    }

    public void DistributeExperience()
    {
        var list = EntityListPool.Get();

        var mon = Character.Entity.Get<Monster>();
        var exp = mon.MonsterBase.Exp;

        if (Character.Map == null)
            return;

        Character.Map.GatherPlayersInRange(Character.Position, 12, list, false);
        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);

        foreach (var e in list)
        {
            if (e.IsNull() || !e.IsAlive())
                continue;

            var player = e.Get<Player>();

            var level = player.GetData(PlayerStat.Level);

            if (level >= 99)
                continue;

            var curExp = player.GetData(PlayerStat.Experience);
            var requiredExp = DataManager.ExpChart.ExpRequired[level];

            if (exp > requiredExp)
                exp = requiredExp; //cap to 1 level per kill

            CommandBuilder.SendExpGain(player, exp);

            curExp += exp;

            if (curExp < requiredExp)
            {
                player.SetData(PlayerStat.Experience, curExp);
                continue;
            }

            while (curExp >= requiredExp && level < 99)
            {
                curExp -= requiredExp;

                player.LevelUp();
                level++;

                if (level < 99)
                    requiredExp = DataManager.ExpChart.ExpRequired[level];
            }

            player.SetData(PlayerStat.Experience, curExp);

            CommandBuilder.LevelUp(player.Character, level, curExp);
            CommandBuilder.SendHealMulti(player.Character, 0, HealType.None);
            CommandBuilder.ChangeSpValue(player, player.GetStat(CharacterStat.Sp), player.GetStat(CharacterStat.MaxSp));
        }
        CommandBuilder.ClearRecipients();
        EntityListPool.Return(list);
    }
    private void FinishCasting()
    {
        IsCasting = false;

        if (Character.Type == CharacterType.Player && !Player.TakeSpForSkill(CastingSkill.Skill, CastingSkill.Level))
        {
            CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
            return;
        }

        SkillHandler.ExecuteSkill(CastingSkill, this);
        if (Character.Type == CharacterType.Monster)
            Character.Monster.RunCastSuccessEvent();
    }

    public void ResumeQueuedSkillAction()
    {
        Character.QueuedAction = QueuedAction.None;

        if (QueuedCastingSkill.Level <= 0)
            return;

        if (QueuedCastingSkill.TargetedPosition != Position.Invalid)
        {
            AttemptStartGroundTargetedSkill(QueuedCastingSkill.TargetedPosition, QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime, QueuedCastingSkill.HideName);
            return;
        }

        var target = QueuedCastingSkill.TargetEntity.GetIfAlive<CombatEntity>();
        if (target == null) return;

        if (target == this)
        {
            AttemptStartSelfTargetSkill(QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime, QueuedCastingSkill.HideName);
            return;
        }

        AttemptStartSingleTargetSkillAttack(target, QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime, QueuedCastingSkill.HideName);
    }

    public void QueueCast(SkillCastInfo skillInfo)
    {
        QueuedCastingSkill = skillInfo;
        Character.QueuedAction = QueuedAction.Cast;
    }

    public bool AttemptStartGroundTargetedSkill(Position target, CharacterSkill skill, int level, float castTime = -1, bool hideSkillName = false)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError($"Cannot attempt a skill action {skill} while dead! " + Environment.StackTrace);
            return false;
        }

        var skillInfo = new SkillCastInfo()
        {
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = target,
            Range = (sbyte)SkillHandler.GetSkillRange(this, skill, level),
            IsIndirect = false,
            HideName = hideSkillName
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

            Character.StopMovingImmediately();
            //QueueCast(skillInfo);

            //return true;
        }

        if (Character.Type == CharacterType.Player && Character.Position.DistanceTo(target) > skillInfo.Range) //if we are out of range, try to move closer
        {
            Character.Player.ClearTarget();
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
        if (target.IsValid())
            Character.FacingDirection = DistanceCache.Direction(Character.Position, target);

        if (castTime < 0f)
            castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, null, skillInfo.Level);
        if (castTime <= 0)
            ExecuteQueuedSkillAttack();
        else
        {
            IsCasting = true;
            CastingTime = Time.ElapsedTimeFloat + castTime;
            if (Character.Type == CharacterType.Player) //monsters have their interrupt mode set during their AI skill handler
            {
                var skillData = DataManager.SkillData[skill];
                CastInterruptionMode = skillData.InterruptMode == CastInterruptionMode.Default ? CastInterruptionMode.InterruptOnSkill : skillData.InterruptMode;
            }

            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.StartCastGroundTargetedMulti(Character, target, skillInfo.Skill, skillInfo.Level, skillInfo.Range, castTime, hideSkillName);
            CommandBuilder.ClearRecipients();
        }
        return true;
    }

    public bool AttemptStartSelfTargetSkill(CharacterSkill skill, int level, float castTime = -1f, bool hideSkillName = false)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError($"Cannot attempt a skill action {skill} while dead! " + Environment.StackTrace);
            return false;
        }

        if (level <= 0)
            level = 10; //you really need to verify they have the skill or not

        var skillInfo = new SkillCastInfo()
        {
            TargetEntity = Entity,
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = Position.Invalid,
            IsIndirect = false,
            HideName = hideSkillName
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
            IsCasting = true;
            CastingTime = Time.ElapsedTimeFloat + castTime;
            if (Character.Type == CharacterType.Player) //monsters have their interrupt mode set during their AI skill handler
            {
                var skillData = DataManager.SkillData[skill];
                CastInterruptionMode = skillData.InterruptMode == CastInterruptionMode.Default ? CastInterruptionMode.InterruptOnSkill : skillData.InterruptMode;
            }

            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            var clientSkill = skillInfo.Skill;
            CommandBuilder.StartCastMulti(Character, null, clientSkill, skillInfo.Level, castTime, hideSkillName);
            CommandBuilder.ClearRecipients();
        }
        return true;
    }

    public bool AttemptStartSingleTargetSkillAttack(CombatEntity target, CharacterSkill skill, int level, float castTime = -1f, bool hideSkillName = false)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError("Cannot attempt a skill action while dead! " + Environment.StackTrace);
            return false;
        }

        if (Character.Type == CharacterType.Player && !Character.Player.VerifyCanUseSkill(skill, level))
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
            HideName = hideSkillName
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

        if (Character.Position.DistanceTo(target.Character.Position) > skillInfo.Range) //if we are out of range, try to move closer
        {
            if (Character.Type == CharacterType.Player)
            {
                Character.Player.ClearTarget();
                if (Character.InMoveLock && Character.MoveLockTime > Time.ElapsedTimeFloat) //we need to queue a cast so we both move and cast once the lock ends
                {
                    QueueCast(skillInfo);
                    return true;
                }
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

        var res = SkillHandler.ValidateTarget(skillInfo, this);

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
            if (Character != target.Character)
                Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);

            if (castTime < 0f)
                castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, target, skillInfo.Level);
            if (castTime <= 0)
                ExecuteQueuedSkillAttack();
            else
            {
                IsCasting = true;
                CastingTime = Time.ElapsedTimeFloat + castTime;
                if (Character.Type == CharacterType.Player) //monsters have their interrupt mode set during their AI skill handler
                {
                    var skillData = DataManager.SkillData[skill];
                    CastInterruptionMode = skillData.InterruptMode == CastInterruptionMode.Default ? CastInterruptionMode.InterruptOnSkill : skillData.InterruptMode;
                }

                Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
                CommandBuilder.StartCastMulti(Character, target.Character, skillInfo.Skill, skillInfo.Level, castTime, hideSkillName);
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
#if DEBUG
            ServerLogger.Log($"Character {Character} failed a queued skill attack with the validation result: {res}");
            return;
#endif
        }

        if (Character.Type == CharacterType.Player && !Player.TakeSpForSkill(CastingSkill.Skill, CastingSkill.Level))
        {
            CommandBuilder.SkillFailed(Player, SkillValidationResult.InsufficientSp);
            return;
        }

        SkillHandler.ExecuteSkill(CastingSkill, this);
        CastingSkill.Clear();
        QueuedCastingSkill.Clear();
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

        var overrideElement = (CharacterElement)GetStat(CharacterStat.OverrideElement);
        if (overrideElement != 0 && overrideElement != CharacterElement.None)
            element = overrideElement;
        return element;
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

        var luckMod = 100;
        var successRate = chance / (float)outOf;
        if (successRate > 0.1f)
            luckMod += (int)((successRate * 1000 - 100) / 4f);

        var attackerLuck = GetEffectiveStat(CharacterStat.Luck);
        var provokerLuck = target.GetEffectiveStat(CharacterStat.Luck);
        if (provokerLuck < 0) provokerLuck = 0;

        var realChance = chance * 10 * (attackerLuck + luckMod) / (provokerLuck + luckMod);
        if (realChance <= 0)
            return false;

        return GameRandom.NextInclusive(0, outOf * 10) < realChance;
    }

    /// <summary>
    /// Test to see if this character is able to hit the enemy.
    /// </summary>
    /// <returns>Returns true if the attack hits</returns>
    public bool TestHitVsEvasion(CombatEntity target, int attackerHitBonus = 0, int defenderFleeBonus = 0)
    {
        var attackerHit = GetStat(CharacterStat.Level) + GetEffectiveStat(CharacterStat.Dex) + GetStat(CharacterStat.AddHit);

        var defenderAgi = target.GetEffectiveStat(CharacterStat.Agi);
        var defenderFlee = target.GetStat(CharacterStat.Level) + defenderAgi + target.GetStat(CharacterStat.AddFlee);

        var hitSuccessRate = attackerHit + attackerHitBonus + 75 - defenderFlee - defenderFleeBonus;
        if (hitSuccessRate < 5) hitSuccessRate = 5;
        if (hitSuccessRate > 95 && target.Character.Type == CharacterType.Player) hitSuccessRate = 95;

        return hitSuccessRate > GameRandom.Next(0, 100);
    }

    public DamageInfo PrepareTargetedSkillResult(CombatEntity target, CharacterSkill skillSource = CharacterSkill.None)
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
            Target = target.Entity,
            AttackSkill = skillSource,
            Time = Time.ElapsedTimeFloat + spriteTiming,
            AttackMotionTime = spriteTiming,
            AttackPosition = Character.Position,
            Flags = DamageApplicationFlags.None
        };

        return di;
    }

    public DamageInfo CalculateCombatResultUsingSetAttackPower(CombatEntity target, int atk1, int atk2, float attackMultiplier, int hitCount,
                                            AttackFlags flags, CharacterSkill skillSource = CharacterSkill.None, AttackElement attackElement = AttackElement.None)
    {
#if DEBUG
        if (!target.IsValidTarget(this, flags.HasFlag(AttackFlags.CanHarmAllies)))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling CalculateCombatResultUsingSetAttackPower.");
#endif

        var baseDamage = GameRandom.NextInclusive(atk1, atk2);

        var eleMod = 100;
        if (!flags.HasFlag(AttackFlags.NoElement))
        {
            var defenderElement = target.GetElement();

            if (attackElement == AttackElement.None)
            {
                if (Character.Type == CharacterType.Player)
                    attackElement = Player.Equipment.WeaponElement;
                else
                    attackElement = AttackElement.Neutral; //for now default to neutral, but we should pull from the weapon here if none is set

            }

            if (defenderElement != CharacterElement.None)
                eleMod = DataManager.ElementChart.GetAttackModifier(attackElement, defenderElement);
        }

        var racialMod = 100;
        if (!flags.HasFlag(AttackFlags.NoDamageModifiers))
        {
            if (target.Entity.Type == EntityType.Player) //monsters can't get race reductions
            {
                var sourceRace = GetRace();
                switch (sourceRace)
                {
                    case CharacterRace.Demon:
                        racialMod -= target.GetStat(CharacterStat.ReductionFromDemon);
                        break;
                    case CharacterRace.Undead:
                        racialMod -= target.GetStat(CharacterStat.ReductionFromUndead);
                        break;
                }
            }

            if (Entity.Type == EntityType.Player) //only players will get bonus damage against a target race
            {
                var targetRace = target.GetRace();

                switch (targetRace)
                {
                    case CharacterRace.Demon:
                        racialMod += GetStat(CharacterStat.PercentVsDemon);
                        break;
                    case CharacterRace.Undead:
                        racialMod += GetStat(CharacterStat.PercentVsUndead);
                        break;
                }
            }

            if (Character.Type == CharacterType.Player)
            {
                var mastery = GetStat(CharacterStat.WeaponMastery);
                attackMultiplier *= (1 + (mastery / 100f));
            }
        }

        var evade = false;
        var isCrit = false;

        if (flags.HasFlag(AttackFlags.Physical) && !flags.HasFlag(AttackFlags.IgnoreEvasion))
            evade = !TestHitVsEvasion(target);

        if (flags.HasFlag(AttackFlags.CanCrit))
        {
            var critRate = 1 + GetEffectiveStat(CharacterStat.Luck) / 3 + GetStat(CharacterStat.Level) / 5;
            var counterCrit = target.GetEffectiveStat(CharacterStat.Luck) / 5 + GetStat(CharacterStat.Level) / 7;
            if (GameRandom.NextInclusive(0, 100) <= critRate - counterCrit)
                isCrit = true;
        }

        if (flags.HasFlag(AttackFlags.GuaranteeCrit))
            isCrit = true;

        if (isCrit)
        {
            baseDamage = atk2;
            evade = false;
            isCrit = true;
            flags |= AttackFlags.IgnoreDefense;
        }

        var defCut = 1f;
        var subDef = 0f;
        if (flags.HasFlag(AttackFlags.Physical) && !flags.HasFlag(AttackFlags.IgnoreDefense))
        {
            var def = target.GetEffectiveStat(CharacterStat.Def);
            defCut = MathF.Pow(0.99f, def - 1);
            subDef = target.GetEffectiveStat(CharacterStat.Vit) * 0.7f;
            if (def > 900)
                subDef = 999999;
        }

        if (flags.HasFlag(AttackFlags.Magical) && !flags.HasFlag(AttackFlags.IgnoreDefense))
        {
            var mDef = target.GetEffectiveStat(CharacterStat.MDef);
            defCut = MathF.Pow(0.99f, mDef - 1);
            subDef = target.GetEffectiveStat(CharacterStat.Int) * 0.7f;
            if (mDef > 900)
                subDef = 999999;
        }


        var damage = (int)(baseDamage * attackMultiplier * (eleMod / 100f) * (racialMod / 100f) * defCut - subDef);
        if (damage < 1)
            damage = 1;

        var lvCut = 1f;
        if (target.Character.Type == CharacterType.Monster)
        {
            //players deal 1.5% less damage per level they are below a monster, to a max of -90%
            lvCut -= 0.015f * (target.GetStat(CharacterStat.Level) - GetStat(CharacterStat.Level));
            lvCut = Math.Clamp(lvCut, 0.1f, 1f);
        }
        else
        {
            //monsters deal 0.5% less damage per level they are below the player, to a max of -50%
            lvCut -= 0.005f * (target.GetStat(CharacterStat.Level) - GetStat(CharacterStat.Level));
            lvCut = Math.Clamp(lvCut, 0.5f, 1f);
        }

        damage = (int)(lvCut * damage);

        if (damage < 1)
            damage = 1;

        if (eleMod == 0 || evade)
            damage = 0;

        var res = AttackResult.NormalDamage;
        if (damage == 0)
            res = AttackResult.Miss;

        if (res == AttackResult.NormalDamage && isCrit)
            res = AttackResult.CriticalDamage;

        var di = PrepareTargetedSkillResult(target, skillSource);
        di.Result = res;
        di.Damage = damage;
        di.HitCount = (byte)hitCount;

        if (statusContainer != null)
            statusContainer.OnAttack(ref di);

        if (target.TryGetStatusContainer(out var targetStatus))
            targetStatus.OnTakeDamage(ref di);

        return di;
    }

    public DamageInfo CalculateCombatResult(CombatEntity target, float attackMultiplier, int hitCount,
                                            AttackFlags flags, CharacterSkill skillSource = CharacterSkill.None, AttackElement element = AttackElement.None)
    {
#if DEBUG
        if (!target.IsValidTarget(this, flags.HasFlag(AttackFlags.CanHarmAllies)))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling CalculateCombatResult.");
#endif

        var isMagic = flags.HasFlag(AttackFlags.Magical);

        var atk1 = !flags.HasFlag(AttackFlags.Magical) ? GetStat(CharacterStat.Attack) : GetStat(CharacterStat.MagicAtkMin);
        var atk2 = !flags.HasFlag(AttackFlags.Magical) ? GetStat(CharacterStat.Attack2) : GetStat(CharacterStat.MagicAtkMax);

        if (Character.Type == CharacterType.Player)
        {
            if (!isMagic)
            {
                var mainStat = GetEffectiveStat(Player.WeaponClass == 12 ? CharacterStat.Dex : CharacterStat.Str);
                var secondaryStat = GetEffectiveStat(Player.WeaponClass == 12 ? CharacterStat.Str : CharacterStat.Dex);

                var statAtk = GetStat(CharacterStat.AddAttackPower) + mainStat + (secondaryStat / 5) + (mainStat / 10) * (mainStat / 10);
                var statWeaponBonus = mainStat / 400f;
                var attackPercent = 1f + (GetStat(CharacterStat.AddAttackPercent) / 100f);
                atk1 = (int)((statAtk + atk1 * (1 + statWeaponBonus)) * attackPercent);
                atk2 = (int)((statAtk + atk2 * (1 + statWeaponBonus)) * attackPercent);
            }
            else
            {
                var matkStat = GetEffectiveStat(CharacterStat.Int);
                var addMatk = GetStat(CharacterStat.AddMagicAttackPower);
                var statMatkMin = addMatk + matkStat + (matkStat / 7) * (matkStat / 7);
                var statMatkMax = addMatk + matkStat + (matkStat / 5) * (matkStat / 5);
                var statWeaponBonus = matkStat / 400f;
                var magicPercent = 1f + (GetStat(CharacterStat.AddMagicAttackPercent) / 100f);
                atk1 = (int)((statMatkMin + atk1 * (1 + statWeaponBonus)) * magicPercent);
                atk2 = (int)((statMatkMax + atk2 * (1 + statWeaponBonus)) * magicPercent);
            }
        }
        else
        {
            if (!isMagic)
            {
                var attackPercent = 1f + (GetStat(CharacterStat.AddAttackPercent) / 100f);
                atk1 = (int)(atk1 * attackPercent);
                atk2 = (int)(atk2 * attackPercent);
            }
            else
            {
                var magicPercent = 1f + (GetStat(CharacterStat.AddMagicAttackPercent) / 100f);
                atk1 = (int)(atk1 * magicPercent);
                atk2 = (int)(atk2 * magicPercent);
            }
        }

        if (atk1 <= 0)
            atk1 = 1;
        if (atk2 < atk1)
            atk2 = atk1;

        return CalculateCombatResultUsingSetAttackPower(target, atk1, atk2, attackMultiplier, hitCount, flags, skillSource, element);
    }

    public void ApplyCooldownForAttackAction(CombatEntity target)
    {
#if DEBUG
        if (!target.IsValidTarget(this))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling PerformAttackAction.");
#endif
        ApplyCooldownForAttackAction();

        Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);
    }

    public void ApplyCooldownForAttackAction(Position target)
    {
        ApplyCooldownForAttackAction();

        Character.FacingDirection = DistanceCache.Direction(Character.Position, target);
    }

    public void ApplyCooldownForSupportSkillAction(float minCooldownTime = 0f)
    {
        if (Character.Type != CharacterType.Player)
        {
            ApplyCooldownForAttackAction(); //players override motion time on support skills, but monsters should not
            return;
        }

        var motionTime = 0.5f;
        var realDelayTime = 0.5f;

        var attackMotionTime = GetTiming(TimingStat.AttackMotionTime); //time for actual weapon strike to occur
        var delayTime = GetTiming(TimingStat.AttackDelayTime); //time before you can attack again

        if (delayTime < minCooldownTime)
            delayTime = minCooldownTime;

        if (attackMotionTime < motionTime)
            motionTime = attackMotionTime;
        if (delayTime < realDelayTime)
            realDelayTime = delayTime;

        if (motionTime > realDelayTime)
            realDelayTime = motionTime;

        if (Character.AttackCooldown + Time.DeltaTimeFloat + 0.005f < Time.ElapsedTimeFloat)
            Character.AttackCooldown = Time.ElapsedTimeFloat + realDelayTime; //they are consecutively attacking
        else
            Character.AttackCooldown += realDelayTime;

        if (Character.Type == CharacterType.Monster)
            Character.Monster.AddDelay(motionTime);

        Character.AddMoveLockTime(motionTime);
    }

    public void ApplyCooldownForAttackAction()
    {
        var attackMotionTime = GetTiming(TimingStat.AttackMotionTime); //time for actual weapon strike to occur
        var delayTime = GetTiming(TimingStat.AttackDelayTime); //time before you can attack again

        if (attackMotionTime > delayTime)
            delayTime = attackMotionTime;

        if (Character.AttackCooldown + Time.DeltaTimeFloat + 0.005f < Time.ElapsedTimeFloat)
            Character.AttackCooldown = Time.ElapsedTimeFloat + delayTime; //they are consecutively attacking
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
            Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
            CommandBuilder.AttackMulti(Character, target.Character, damageInfo, showAttackMotion);
            CommandBuilder.ClearRecipients();
        }

        if (damageInfo.Damage != 0)
        {
            //if(damageInfo.Time < Time.ElapsedTimeFloat)
            //    target.ApplyQueuedCombatResult(ref damageInfo);
            //else
            target.QueueDamage(damageInfo);
        }

        //if (target.Character.Type == CharacterType.Monster && damageInfo.Damage > target.GetStat(CharacterStat.Hp))
        //{
        //    var mon = target.Entity.Get<Monster>();
        //    mon.AddDelay(GetTiming(TimingStat.SpriteAttackTiming) * 2); //make sure it stops acting until it dies
        //}
    }

    public void PerformMeleeAttack(CombatEntity target)
    {
        ApplyCooldownForAttackAction(target);

        var flags = AttackFlags.Physical;
        if (Character.Type == CharacterType.Player)
            flags |= AttackFlags.CanCrit;

        var di = CalculateCombatResult(target, 1f, 1, flags);
        if (di.Result != AttackResult.CriticalDamage && Character.Type == CharacterType.Player)
        {
            var doubleChance = GetStat(CharacterStat.DoubleAttackChance);
            if (doubleChance > 0 && GameRandom.Next(0, 100) <= doubleChance)
                di.HitCount = 2;
        }

        ExecuteCombatResult(di);
    }

    public void CancelCast()
    {
        if (!IsCasting)
            return;
        IsCasting = false;
        if (!Character.HasVisiblePlayers())
            return;

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.StopCastMulti(Character);
        CommandBuilder.ClearRecipients();

    }

    private void ApplyQueuedCombatResult(ref DamageInfo di)
    {

        if (Character.State == CharacterState.Dead || !Entity.IsAlive() || Character.IsTargetImmune || Character.Map == null)
            return;

        //if (di.Source.IsAlive() && di.Source.TryGet<WorldObject>(out var enemy))
        //{
        //    if (!enemy.IsActive || enemy.Map != Character.Map)
        //        continue;
        //    if (enemy.State == CharacterState.Dead)
        //        continue; //if the attacker is dead we bail

        //    //if (enemy.Position.SquareDistance(Character.Position) > 31)
        //    //    continue;
        //}
        //else
        //    continue;

        if (Character.State == CharacterState.Sitting)
            Character.State = CharacterState.Idle;

        var damage = di.Damage * di.HitCount;

        //inform clients the player was hit and for how much
        var delayTime = GetTiming(TimingStat.HitDelayTime);

        var knockback = di.KnockBack;
        var hasHitStop = !di.Flags.HasFlag(DamageApplicationFlags.NoHitLock);

        if (Character.Type == CharacterType.Monster && delayTime > 0.15f)
            delayTime = 0.15f;
        if (!hasHitStop)
            delayTime = 0f;
        var oldPosition = Character.Position;

        var sendMove = di.Flags.HasFlag(DamageApplicationFlags.UpdatePosition);

        if (Character.Type == CharacterType.Monster && knockback > 0 && Character.Monster.MonsterBase.Special == CharacterSpecialType.Boss)
        {
            knockback = 0;
            delayTime = 0.03f;
            sendMove = true;
        }

        Character.AddMoveLockTime(delayTime);

        if (knockback > 0)
        {
            var pos = Character.Map.WalkData.CalcKnockbackFromPosition(Character.Position, di.AttackPosition, di.KnockBack);
            if (Character.Position != pos)
                Character.Map.ChangeEntityPosition3(Character, Character.WorldPosition, pos, false);
            sendMove = false;
        }

        if (Character.Type == CharacterType.Monster)
            Character.Monster.NotifyOfAttack(ref di);

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        if (sendMove)
            CommandBuilder.SendMoveEntityMulti(Character);
        CommandBuilder.SendHitMulti(Character, damage, hasHitStop);

        if (IsCasting)
        {
            if (  (CastInterruptionMode == CastInterruptionMode.InterruptOnDamage && di.Damage > 0) 
               || (CastInterruptionMode != CastInterruptionMode.NeverInterrupt && di.KnockBack > 0)
               || (CastInterruptionMode == CastInterruptionMode.InterruptOnSkill && di.AttackSkill != CharacterSkill.None))
            {
                //character casts aren't interrupted by attacks if they are close to executing
                if (Character.Type != CharacterType.Player || CastTimeRemaining > 0.3f)
                {
                    IsCasting = false;
                    CommandBuilder.StopCastMulti(Character);
                }
            }
        }

        CommandBuilder.ClearRecipients();

        if (!di.Target.IsNull() && di.Source.IsAlive())
            Character.LastAttacked = di.Source;

        var ec = di.Target.Get<CombatEntity>();
        var hp = GetStat(CharacterStat.Hp);
        hp -= damage;

        SetStat(CharacterStat.Hp, hp);


        if (hp <= 0)
        {
            ResetSkillDamageCooldowns();

            if (Character.Type == CharacterType.Monster)
            {
                Character.ResetState();
                var monster = Entity.Get<Monster>();

                monster.CallDeathEvent();

                if (GetStat(CharacterStat.Hp) > 0)
                {
                    //death is cancelled!
                    return;
                }

                if (di.Source.IsAlive() && di.Source.TryGet<WorldObject>(out var attacker) && attacker.Type == CharacterType.Player && DataManager.MvpMonsterCodes.Contains(monster.MonsterBase.Code))
                {
                    //if we're an mvp, give the attacker the effect
                    Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
                    CommandBuilder.SendEffectOnCharacterMulti(attacker, DataManager.EffectIdForName["MVP"]);
                    CommandBuilder.ClearRecipients();
                }

                monster.Die();
                DamageQueue.Clear();

                return;
            }

            if (Character.Type == CharacterType.Player)
            {
                SetStat(CharacterStat.Hp, 0);
                Player.Die();
                return;
            }

            return;
        }

        if (oldPosition != Character.Position)
            Character.Map?.TriggerAreaOfEffectForCharacter(Character, oldPosition, Character.Position);
    }

    private void AttackUpdate()
    {
        while (DamageQueue.Count > 0 && DamageQueue[0].Time < Time.ElapsedTimeFloat)
        {
            var di = DamageQueue[0];
            DamageQueue.RemoveAt(0);

            ApplyQueuedCombatResult(ref di);
        }
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

        if (statusContainer != null)
            statusContainer.UpdateStatusEffects();

        if (DamageQueue.Count > 0)
            AttackUpdate();
    }
}
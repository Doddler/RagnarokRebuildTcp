using Microsoft.Extensions.Logging;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;
using System;
using System.Diagnostics;
using System.Threading.Channels;
using System.Xml.Linq;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Simulation.Pathfinding;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.EntityComponents.Monsters;

public class MonsterSkillAiState(Monster monsterIn)
{
    private readonly Monster monster = monsterIn;
    public Action<MonsterSkillAiState>? CastSuccessEvent = null;
    public bool SkillCastSuccess;
    public bool ExecuteEventAtStartOfCast;
    public bool FinishedProcessing;
    //public CharacterSkillB Test;
    public int HpPercent => monster.CombatEntity.GetStat(CharacterStat.Hp) * 100 / monster.CombatEntity.GetStat(CharacterStat.MaxHp);
    public int HpValue => monster.CombatEntity.GetStat(CharacterStat.Hp);
    public int MinionCount => monster.ChildCount;
    public bool InventoryFull => monster.IsInventoryFull;
    private WorldObject? targetForSkill = null;
    //private bool failNextSkill = false;

    public EntityList? Events;
    public int EventsCount => Events?.Count ?? 0;

    public Position CurrentPosition => monster.Character.Position;

    private Dictionary<string, float>? specialCooldowns;
    private int[]? stateFlags;
    

    public CharacterSkill LastDamageSourceType => monster.LastDamageSourceType;
    public bool CanSeePlayers => monster.Character.HasVisiblePlayers();

    public int DistanceToSelectedTarget => targetForSkill == null ? -1 : monster.Character.Position.Distance(TargetPosition);
    public Position TargetPosition => targetForSkill?.Position ?? Position.Invalid;
    public int TimeAlive => (int)(monster.TimeAlive * 1000);

    public bool IsMasterAlive => monster.HasMaster && monster.GetMaster().TryGet<WorldObject>(out var chara) && chara.IsActive &&  chara.State != CharacterState.Dead;

    public void SetStateFlag(int id, int value)
    {
        if (stateFlags == null)
            stateFlags = new int[4];
        stateFlags[id] = value;
    }

    public int GetStateFlag(int id)
    {
        if (stateFlags == null)
            return 0;
        return stateFlags[id];
    }

    public bool IsDisabled() => monster.GetStat(CharacterStat.Disabled) > 0;
    
    //public void Debug(string hello) { ServerLogger.Log(hello); }

    public int Random(int low, int high) => GameRandom.NextInclusive(low, high);
    public int Random(int high) => GameRandom.NextInclusive(high);

    public void Die(bool giveExperience = true)
    {
        monster.Die(giveExperience);
    }

    public void Leave()
    {
        monster.Die(false, false, CharacterRemovalReason.Teleport);
    }

    public void FlagAsMvp()
    {
        monster.Character.IsImportant = true;
        monster.Character.DisplayType = CharacterDisplayType.Mvp;
        monster.Character.Map?.RegisterImportantEntity(monster.Character);
    }

    public void EnterPostDeathPhase()
    {
        var hp = monster.CombatEntity.GetStat(CharacterStat.Hp);
        var map = monster.Character.Map;
        if (hp > 0)
        {
            ServerLogger.LogWarning($"Monster {monster.Character} on map {map?.Name} called CancelDeath but it is not actually at 0 hp!");
        }

        monster.CurrentAiState = MonsterAiState.StateSpecial;
        monster.CombatEntity.IsTargetable = false;
        monster.CombatEntity.DamageQueue.Clear();
        monster.CombatEntity.SetStat(CharacterStat.Hp, 1);
        monster.CombatEntity.IsCasting = false;
        monster.CombatEntity.ResetSkillCooldowns();
        monster.Character.QueuedAction = QueuedAction.None;
        monster.Character.StopMovingImmediately();


        map?.AddVisiblePlayersAsPacketRecipients(monster.Character);
        CommandBuilder.ChangeCombatTargetableState(monster.Character, false);
        CommandBuilder.ClearRecipients();
    }

    public void HealMyMaster(int val) => HealMyMaster(val, val);
    public void HealMyMaster(int min, int max)
    {
        if (!IsMasterAlive)
            return;
        if (!monster.GetMaster().TryGet<WorldObject>(out var master))
            return;

        var heal = min;
        if(min != max)
            heal = GameRandom.NextInclusive(min, max);

        var res = monster.CombatEntity.PrepareTargetedSkillResult(master.CombatEntity, CharacterSkill.Heal);
        res.Damage = -heal;
        res.Result = AttackResult.Heal;
        res.AttackMotionTime = 0;
        res.HitCount = 1;
        
        master.CombatEntity.HealHp(heal);
        
        monster.Character.Map?.AddVisiblePlayersAsPacketRecipients(master);
        CommandBuilder.SkillExecuteTargetedSkill(monster.Character, master, CharacterSkill.Heal, 10, res);
        CommandBuilder.SendHealMulti(master, heal, HealType.None);
        CommandBuilder.ClearRecipients();
    }

    private bool SkillFail()
    {
        SkillCastSuccess = false;
        targetForSkill = null;
        return false;
    }

    public bool SkillSuccess()
    {
        SkillCastSuccess = true;
        targetForSkill = null;
        monster.CastSuccessEvent = null;
        CastSuccessEvent = null;
        FinishedProcessing = true;
        return true;
    }

    public bool IsNamedEventOffCooldown(string name)
    {
        if (specialCooldowns == null || !specialCooldowns.TryGetValue(name, out var cooldown))
            return true;

        return Time.ElapsedTimeFloat > cooldown;
    }

    public void SetEventCooldown(string name, float cooldown)
    {
        if (specialCooldowns == null)
            specialCooldowns = new Dictionary<string, float>();

        if (cooldown < 0)
            specialCooldowns[name] = float.MaxValue;
        else
            specialCooldowns[name] = Time.ElapsedTimeFloat + cooldown / 1000f;
    }

    public void ResetAllCooldowns()
    {
        specialCooldowns?.Clear();
        monster.CombatEntity.ResetSkillCooldowns();
    }

    public void PutSkillOnCooldown(CharacterSkill skill, int cooldown)
    {
        monster.CombatEntity.SetSkillCooldown(skill, cooldown / 1000f);
    }

    public void ChangeAiClass(MonsterAiType type, bool resetState = true)
    {
        monster.ChangeAiStateMachine(type);
        if (resetState)
        {
            ChangeAiState(MonsterAiState.StateIdle);
            monster.ResetIdleWaitTime();
        }
    }

    public void ChangeAiState(MonsterAiState state)
    {
        monster.CurrentAiState = state;
        monster.UpdateStateChangeTime();
    }

    public void ChangeAiHandler(string aiType)
    {
        monster.ChangeAiSkillHandler(aiType);
    }

    public void SetGivesExperience(bool gives)
    {
        monster.GivesExperience = gives;
    }

    public int TimeInAiState => (int)(monster.TimeInCurrentAiState * 1000);
    public int TimeOutOfCombat => (int)(monster.DurationOutOfCombat * 1000);
    public int TimeSinceLastDamage => (int)(monster.TimeSinceLastDamage * 1000);
    public bool WasRangedAttacked => monster.WasAttacked && monster.LastAttackRange > 4;
    public MonsterAiState PreviousAiState => monster.PreviousAiState;

    public void AdminHide()
    {
        var ch = monster.Character;
        if (ch.AdminHidden || ch.Map == null)
            return;

        ch.Map.RemoveEntity(ref ch.Entity, CharacterRemovalReason.OutOfSight, false);
        ch.AdminHidden = true;

        monster.PreviousAiState = monster.CurrentAiState;
        monster.CurrentAiState = MonsterAiState.StateHidden;
        monster.UpdateStateChangeTime();

        monster.CombatEntity.ClearDamageQueue();
    }

    public void AdminUnHide()
    {
        var ch = monster.Character;
        if (!ch.AdminHidden)
            return;

        if (ch.Map == null)
            throw new Exception($"Monster {ch} attempting to execute AdminUnHide, but the npc is not currently attached to a map.");

        ch.AdminHidden = false;
        ch.Map.AddEntity(ref ch.Entity, false);

        monster.CurrentAiState = monster.PreviousAiState;
        monster.PreviousAiState = MonsterAiState.StateHidden;
        monster.UpdateStateChangeTime();
    }

    public void SetHpNoNotify(int hp, bool ignoreMax = false)
    {
        if (!ignoreMax)
        {
            var max = monster.GetStat(CharacterStat.MaxHp);
            if (hp > max)
                hp = max;
        }

        monster.SetStat(CharacterStat.Hp, hp);
    }

    public void TeleportNearRandomMinion(int distance = 5)
    {
        var child = monster.GetRandomChild();
        if (!child.TryGet<WorldObject>(out var target))
            return;

        var ch = monster.Character;
        var pos = monster.Character.Map!.GetRandomVisiblePositionInArea(target.Position, distance / 2, distance);

        if (ch.AdminHidden)
            monster.Character.Position = pos;
        else
            monster.Character.Map.TeleportEntity(ref ch.Entity, ch, pos, CharacterRemovalReason.OutOfSight);
    }

    public void CallHiddenParentToNearbyPosition(int distance = 5)
    {
        if (!monster.HasMaster)
            return;
        var mEntity = monster.GetMaster();
        if (!mEntity.TryGet<WorldObject>(out var master))
            return;
        if (master.Type != CharacterType.Monster)
            return;
        if (!master.AdminHidden)
            return;

        var pos = monster.Character.Map.GetRandomVisiblePositionInArea(monster.Character.Position, distance / 2, distance);
        master.Position = pos;
    }

    //public bool TriggerSelfTargetedSkillEffect(CharacterSkill skill, int level)
    //{
    //    monster.Character.AttackCooldown = 0f; //we always want this to cast
    //    return monster.CombatEntity.AttemptStartSelfTargetSkill(skill, level);
    //}

    public bool CheckCast(CharacterSkill skill, int chance)
    {
        if (monster.CombatEntity.IsSkillOnCooldown(skill))
            return false;

        if (GameRandom.Next(0, 1000) > chance)
            return false;

        return true;
    }

    public void LookAtPosition(Position p)
    {
        //var dir = DistanceCache.Direction(Monster.Character.Position, p);
        monster.Character.ChangeLookDirection(p);
    }

    public bool Cast(CharacterSkill skill, int level, int castTime, int delay = 0, MonsterSkillAiFlags flags = MonsterSkillAiFlags.None)
    {
        var ce = monster.CombatEntity;
        var attr = SkillHandler.GetMonsterSkillAttributes(skill);
        var skillTarget = attr.SkillTarget;
        var range = SkillHandler.GetSkillRange(ce, skill, level);
        if (flags.HasFlag(MonsterSkillAiFlags.UnlimitedRange))
            range = 21;


        var hideSkillName = flags.HasFlag(MonsterSkillAiFlags.HideSkillName);
        var ignoreTargetRequirement = flags.HasFlag(MonsterSkillAiFlags.NoTarget);
        ExecuteEventAtStartOfCast = flags.HasFlag(MonsterSkillAiFlags.EventOnStartCast);
        
        if (flags.HasFlag(MonsterSkillAiFlags.EasyInterrupt))
            ce.CastInterruptionMode = CastInterruptionMode.InterruptOnDamage;
        else if (flags.HasFlag(MonsterSkillAiFlags.NoInterrupt))
            ce.CastInterruptionMode = CastInterruptionMode.NeverInterrupt;
        else
        {
            if (DataManager.SkillData.TryGetValue(skill, out var skillData))
                ce.CastInterruptionMode = skillData.InterruptMode;
            else
                ce.CastInterruptionMode = CastInterruptionMode.InterruptOnKnockback;
        }

        if (skillTarget == SkillTarget.Ground)
        {
            var pos = Position.Zero;
            if (!ignoreTargetRequirement)
            {
                var target = targetForSkill;
                if (target == null || !target.CombatEntity.IsValidTarget(ce))
                    if (monster.Target.TryGet<WorldObject>(out var newTarget))
                        target = newTarget;
                if (target == null)
                {
                    using var list = EntityListPool.Get();
                    monster.Character.Map?.GatherEnemiesInRange(monster.Character, range, list, true, true);
                    if (list.Count <= 0)
                        return SkillFail(); //no enemies in range

                    if (list.Count == 1)
                        target = list[0].Get<WorldObject>();
                    else
                        target = list[GameRandom.Next(0, list.Count)].Get<WorldObject>();
                }

                pos = target.Position;
            }

            if (!ce.AttemptStartGroundTargetedSkill(pos, skill, level, castTime / 1000f, hideSkillName))
                return SkillFail();
            ce.SetSkillCooldown(skill, delay / 1000f);
            return SkillSuccess();
        }

        if (skillTarget == SkillTarget.Ally || (skillTarget == SkillTarget.Any && targetForSkill != null))
        {
            if (targetForSkill != null)
            {
                if (!ce.CanAttackTarget(targetForSkill, range)) return SkillFail();
                if (!ce.AttemptStartSingleTargetSkillAttack(targetForSkill.CombatEntity, skill, level, castTime / 1000f, hideSkillName))
                    return SkillFail();

                ce.SetSkillCooldown(skill, delay / 1000f);
                return SkillSuccess();
            }
            else
                skillTarget = SkillTarget.Self;
        }

        if (skillTarget == SkillTarget.Self || (skillTarget == SkillTarget.Any && targetForSkill == null))
        {
            if (!ce.AttemptStartSelfTargetSkill(skill, level, castTime / 1000f, hideSkillName))
                return SkillFail();
            ce.SetSkillCooldown(skill, delay / 1000f);
            return SkillSuccess();
        }

        if (skillTarget == SkillTarget.Enemy)
        {
            //if our conditional statement selected a target for us, use that, otherwise use our current target
            var target = targetForSkill;
            if (target == null || !target.CombatEntity.IsValidTarget(ce))
                if (monster.Target.TryGet<WorldObject>(out var newTarget))
                    target = newTarget;

            //if we're in a state where we have a target, we only need to check if we can use this skill on that enemy
            if (target != null && !flags.HasFlag(MonsterSkillAiFlags.RandomTarget))
            {
                if (!ce.CanAttackTarget(target, range)) return SkillFail();
                if (!ce.AttemptStartSingleTargetSkillAttack(target.CombatEntity, skill, level, castTime / 1000f))
                    return SkillFail();

                ce.SetSkillCooldown(skill, delay / 1000f);
                return SkillSuccess();
            }

            //if we don't have a target we have to assume we're in a state where we need to get one
            var list = EntityListPool.Get();
            monster.Character.Map?.GatherEnemiesInRange(monster.Character, range, list, true, true);
            if (list.Count <= 0)
            {
                EntityListPool.Return(list);
                return SkillFail(); //no enemies in range
            }

            if (list.Count == 1)
                target = list[0].Get<WorldObject>();
            else
                target = list[GameRandom.Next(0, list.Count)].Get<WorldObject>();

            EntityListPool.Return(list);

            if (!ce.AttemptStartSingleTargetSkillAttack(target.CombatEntity, skill, level, castTime / 1000f))
                return SkillFail();

            ce.SetSkillCooldown(skill, delay / 1000f);
            return SkillSuccess();
        }

        return SkillFail();
    }

    //a named cast has no effect, but it will trigger the post effect event handler where you can add custom results
    public bool TryCast(string cooldownName, int level, int chance, int castTime, int delay, MonsterSkillAiFlags flags = MonsterSkillAiFlags.None)
    {
        if (!IsNamedEventOffCooldown(cooldownName))
            return SkillFail();

        if (GameRandom.Next(0, 1000) > chance)
            return SkillFail();

        var result = Cast(CharacterSkill.None, level, castTime, 0, flags);
        if (result)
        {
            monster.LastDamageSourceType = CharacterSkill.None;
            SetEventCooldown(cooldownName, delay);
        }

        return result;
    }

    public bool TryCast(CharacterSkill skill, int level, int chance, int castTime, int delay, MonsterSkillAiFlags flags = MonsterSkillAiFlags.None)
    {
        if (!CheckCast(skill, chance))
            return SkillFail();

        if (skill == CharacterSkill.NoCast)
            flags |= MonsterSkillAiFlags.HideSkillName;

        var result = Cast(skill, level, castTime, delay, flags);
        if (result)
            monster.LastDamageSourceType = CharacterSkill.None;

        return result;
    }

    public void CancelCast()
    {
        monster.CombatEntity.CancelCast();
    }

    public void CallDefaultMinions()
    {
        var monsterDef = monster.MonsterBase;
        if (monsterDef.Minions == null)
        {
            ServerLogger.LogWarning($"Monster {monster.Character.Name} attempting to call default minions, but has none defined.");
            return;
        }

        var map = monster.Character.Map;

        if (monsterDef.Minions != null && monsterDef.Minions.Count > 0)
        {
            for (var i = 0; i < monsterDef.Minions.Count; i++)
            {
                var minionDef = monsterDef.Minions[i];
                for (var j = 0; j < minionDef.Count; j++)
                {
                    var minion = World.Instance.CreateMonster(map, minionDef.Monster, Area.CreateAroundPoint(monster.Character.Position, 3), null);
                    var minionMonster = minion.Get<Monster>();
                    minionMonster.ResetAiUpdateTime();
                    minionMonster.GivesExperience = false;

                    monster.AddChild(ref minion, MonsterAiType.AiMinion);
                }
            }
        }
    }

    public void SummonMinions(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(monster.Character.Map != null, $"Npc {monster.Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(monster.Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(monster.Character.Map, monsterDef, Area.CreateAroundPoint(monster.Character.Position, 3), null);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            minionMonster.GivesExperience = false;

            monster.AddChild(ref minion, MonsterAiType.AiMinion);
        }
    }


    public void SummonMonstersNoExp(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(monster.Character.Map != null, $"Npc {monster.Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(monster.Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(monster.Character.Map, monsterDef, area, null);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            minionMonster.SetMaster(monster.Entity); //these monsters have masters but are not minions of the parent

            minionMonster.GivesExperience = false;
        }
    }


    public void SummonMonstersUnderMyMaster(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(monster.Character.Map != null, $"Npc {monster.Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(monster.Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(monster.Character.Map, monsterDef, area, null);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            if(monster.HasMaster)
                minionMonster.SetMaster(monster.GetMaster()); //these monsters have masters but are not minions of the parent

            minionMonster.GivesExperience = false;
        }
    }

    public void TossSummonMonster(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(monster.Character.Map != null, $"Npc {monster.Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(monster.Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(monster.Character.Map, monsterDef, area, null, false);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            minionMonster.AddDelay(0.6f);
            minionMonster.SetMaster(monster.Entity); //these monsters have masters but are not minions of the parent

            minionMonster.GivesExperience = false;

            monster.Character.Map.AddEntityWithEvent(ref minion, CreateEntityEventType.Toss, monster.Character.Position);
        }
    }


    public void TossSummonMonster(int count, string name, string aiType, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(monster.Character.Map != null, $"Npc {monster.Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(monster.Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(monster.Character.Map, monsterDef, area, null, false);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            minionMonster.AddDelay(0.6f);
            minionMonster.SetMaster(monster.Entity); //these monsters have masters but are not minions of the parent

            minionMonster.GivesExperience = false;

            monster.Character.Map.AddEntityWithEvent(ref minion, CreateEntityEventType.Toss, monster.Character.Position);
            minionMonster.ChangeAiSkillHandler(aiType);
        }
    }

    public void TossSummonMinion(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    {
        Debug.Assert(monster.Character.Map != null, $"Npc {monster.Character.Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

        var monsterDef = DataManager.MonsterCodeLookup[name];

        var area = Area.CreateAroundPoint(monster.Character.Position + new Position(offsetX, offsetY), width, height);

        for (var i = 0; i < count; i++)
        {
            var minion = World.Instance.CreateMonster(monster.Character.Map, monsterDef, area, null, false);
            var minionMonster = minion.Get<Monster>();
            minionMonster.ResetAiUpdateTime();
            minionMonster.AddDelay(0.6f);
            //minionMonster.SetMaster(monster.Entity); //these monsters have masters but are not minions of the parent

            minionMonster.GivesExperience = false;

            monster.Character.Map.AddEntityWithEvent(ref minion, CreateEntityEventType.Toss, monster.Character.Position);
            monster.AddChild(ref minion, MonsterAiType.AiMinion);
            //minionMonster.MakeChild(ref monster.Entity);
        }
    }

    public void SendEmote(int emoteId)
    {
        var map = monster.Character.Map;
        map.AddVisiblePlayersAsPacketRecipients(monster.Character);
        CommandBuilder.SendEmoteMulti(monster.Character, emoteId);
        CommandBuilder.ClearRecipients();
    }

    public void SendEmoteFromTarget(int emoteId)
    {
        if (targetForSkill == null)
            return;
        var map = targetForSkill.Map;
        map.AddVisiblePlayersAsPacketRecipients(targetForSkill);
        CommandBuilder.SendEmoteMulti(targetForSkill, emoteId);
        CommandBuilder.ClearRecipients();
    }

    public bool IsTargetMonster(string className)
    {
        if (targetForSkill == null || targetForSkill.Type != CharacterType.Monster)
            return false;

        return targetForSkill.Monster.MonsterBase.Code == className;
    }

    public bool FindAllyBelowHpPercent(int percent)
    {
        targetForSkill = null;
        var map = monster.Character.Map;
        using var pool = EntityListPool.Get();
        
        Debug.Assert(map != null);
        map.GatherAlliesInRange(monster.Character, 9, pool, true);
        if (pool.Count == 0)
            return false;

        var offset = GameRandom.Next(0, pool.Count);

        for (var i = 0; i < pool.Count; i++)
        {
            var e = pool[(i + offset) % pool.Count]; //we start at a random point in the list to make it at least slightly less predictable
            if (e.TryGet<CombatEntity>(out var target))
            {
                if (target.GetStat(CharacterStat.Hp) * 100 / target.GetStat(CharacterStat.MaxHp) < percent)
                {
                    targetForSkill = target.Character;
                    return true;
                }
            }
        }

        return false;
    }

    public Position RandomFreeTileInRange(int range)
    {
        if (monster.Character.Map != null && monster.Character.Map.WalkData.FindWalkableCellInArea(Area.CreateAroundPoint(monster.Character.Position, range), out var pos))
            return pos;

        return monster.Character.Position;
    }

    public bool FindRandomPlayerOnMap()
    {
        Debug.Assert(monster.Character.Map != null);

        var players = monster.Character.Map.Players;
        if(players.Count == 0) return false;

        using var pool = EntityListPool.Get();
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].TryGet<CombatEntity>(out var potentialTarget))
                if(potentialTarget.IsValidTarget(monster.CombatEntity))
                    pool.Add(players[i]);
        }

        if (pool.Count > 0)
        {
            if (pool[GameRandom.Next(0, pool.Count)].TryGet<WorldObject>(out var target))
            {
                targetForSkill = target;
                return true;
            }
        }
        
        return false;
    }

    public void CreateEvent(string eventName, string? valueString = null) => CreateEvent(eventName, monster.Character.Position, 0, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, 0, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, int value2, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, value2, 0, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, int value2, int value3, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, value2, value3, 0, valueString);
    public void CreateEvent(string eventName, Position pos, int value1, int value2, int value3, int value4, string? valueString = null) => CreateEvent(eventName, pos.X, pos.Y, value1, value2, value3, value4, valueString);

    public void CreateEvent(string eventName, int x, int y, string? valueString = null) => CreateEvent(eventName, x, y, 0, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, int x, int y, int value1, string? valueString = null) => CreateEvent(eventName, x, y, value1, 0, 0, 0, valueString);
    public void CreateEvent(string eventName, int x, int y, int value1, int value2, string? valueString = null) => CreateEvent(eventName, x, y, value1, value2, 0, 0, valueString);
    public void CreateEvent(string eventName, int x, int y, int value1, int value2, int value3, string? valueString = null) => CreateEvent(eventName, x, y, value1, value2, value3, 0, valueString);

    public void CreateEvent(string eventName, int x, int y, int value1, int value2, int value3, int value4, string? valueString = null)
    {
        var chara = monster.Character;
        if (chara.Map == null)
            throw new Exception($"Npc {chara.Name} attempting to create event, but the monster is not currently attached to a map.");

        var eventObj = World.Instance.CreateEvent(monster.Entity, chara.Map, eventName, new Position(x, y), value1, value2, value3, value4, valueString);
        eventObj.Get<Npc>().Owner = monster.Entity;
        Events ??= EntityListPool.Get();
        Events.ClearInactive();
        Events.Add(eventObj);

    }
}
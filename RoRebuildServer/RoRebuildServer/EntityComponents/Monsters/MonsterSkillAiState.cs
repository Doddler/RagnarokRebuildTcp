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
using System.Xml.Linq;

namespace RoRebuildServer.EntityComponents.Monsters;

public class MonsterSkillAiState(Monster monster)
{
    public Monster Monster = monster;
    public Action<MonsterSkillAiState>? CastSuccessEvent = null;
    public bool SkillCastSuccess;
    public bool ExecuteEventAtStartOfCast;
    public bool FinishedProcessing;

    public int HpPercent => monster.CombatEntity.GetStat(CharacterStat.Hp) * 100 / monster.CombatEntity.GetStat(CharacterStat.MaxHp);
    public int MinionCount => monster.ChildCount;
    private WorldObject? targetForSkill = null;
    private bool failNextSkill = false;

    private Dictionary<string, float>? specialCooldowns;
    

    public void Debug(string hello) { ServerLogger.Log(hello); }

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

        specialCooldowns[name] = Time.ElapsedTimeFloat + cooldown / 1000f;
    }

    public void ResetAllCooldowns()
    {
        specialCooldowns?.Clear();
        monster.CombatEntity.ResetSkillCooldowns();
    }

    public void ChangeAiState(MonsterAiState state)
    {
        monster.CurrentAiState = state;
    }
    
    public bool TryCast(CharacterSkill skill, int level, int chance, int castTime, int delay, MonsterSkillAiFlags flags = MonsterSkillAiFlags.None)
    {
        if (monster.CombatEntity.IsSkillOnCooldown(skill))
            return SkillFail();

        if (GameRandom.Next(0, 1000) > chance)
            return SkillFail();

        var ce = monster.CombatEntity;
        var attr = SkillHandler.GetSkillAttributes(skill);
        var range = SkillHandler.GetSkillRange(ce, skill, level);

        ExecuteEventAtStartOfCast = flags.HasFlag(MonsterSkillAiFlags.EventOnStartCast);

        if (attr.SkillTarget == SkillTarget.SelfCast)
        {
            if(!ce.StartCastingSelfTargetedSkill(skill, level, castTime / 1000f))
                return SkillFail();
            ce.SetSkillCooldown(skill, delay / 1000f);
            return SkillSuccess();
        }

        if (attr.SkillTarget == SkillTarget.SingleTarget)
        {
            //if our conditional statement selected a target for us, use that, otherwise use our current target
            var target = targetForSkill;
            if (target == null || !target.CombatEntity.IsValidTarget(ce))
                if(monster.Target.TryGet<WorldObject>(out var newTarget))
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

                    monster.AddChild(ref minion);
                }
            }
        }
    }

    //public void SummonMinion(int count, string name, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0)
    //{
    //    var chara = Entity.Get<WorldObject>();

    //    Debug.Assert(chara.Map != null, $"Npc {Name} cannot summon mobs {name} nearby, it is not currently attached to a map.");

    //    var monster = DataManager.MonsterCodeLookup[name];

    //    var area = Area.CreateAroundPoint(chara.Position + new Position(offsetX, offsetY), width, height);

    //    var mobs = Mobs;
    //    if (mobs == null)
    //    {
    //        mobs = new EntityList(count);
    //        Mobs = mobs;
    //    }
    //    else
    //        mobs.ClearInactive();

    //    for (int i = 0; i < count; i++)
    //        mobs.Add(World.Instance.CreateMonster(chara.Map, monster, area, null));
    //}

    public void SendEmote(int emoteId)
    {
        var map = monster.Character.Map;
        map.GatherPlayersForMultiCast(monster.Character);
        CommandBuilder.SendEmoteMulti(monster.Character, emoteId);
        CommandBuilder.ClearRecipients();
    }

    public void SendEmoteFromTarget(int emoteId)
    {
        if (targetForSkill == null)
            return;
        var map = targetForSkill.Map;
        map.GatherPlayersForMultiCast(targetForSkill);
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
        var pool = EntityListPool.Get();
        map.GatherAlliesInRange(monster.Character, 9, pool, true);
        if (pool.Count == 0)
            return false;

        foreach (var e in pool)
        {
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
}
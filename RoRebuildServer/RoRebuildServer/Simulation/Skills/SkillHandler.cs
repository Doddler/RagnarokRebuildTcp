﻿using System.Reflection;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Skills.SkillHandlers;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills;

public static class SkillHandler
{
    private static SkillHandlerBase?[] handlers;
    private static SkillHandlerAttribute[] skillAttributes;
    private static MonsterSkillHandlerAttribute[] monsterSkillAttributes;

    public static SkillHandlerAttribute GetSkillAttributes(CharacterSkill skill) => skillAttributes[(int)skill];
    public static SkillPreferredTarget GetPreferredSkillTarget(CharacterSkill skill) => skillAttributes[(int)skill].SkillPreferredTarget;
    public static MonsterSkillHandlerAttribute GetMonsterSkillAttributes(CharacterSkill skill) => monsterSkillAttributes[(int)skill];

    public static void ApplyPassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.ApplyPassiveEffects(owner, level);
    public static void RemovePassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.RemovePassiveEffects(owner, level);
    public static void RefreshPassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.RefreshPassiveEffects(owner, level);

    static SkillHandler()
    {
        var count = Enum.GetNames(typeof(CharacterSkill)).Length;
        handlers = new SkillHandlerBase[count];
        skillAttributes = new SkillHandlerAttribute[count];
        monsterSkillAttributes = new MonsterSkillHandlerAttribute[count];

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<SkillHandlerAttribute>() != null))
        {
            var handler = (SkillHandlerBase)Activator.CreateInstance(type)!;
            var attr = type.GetCustomAttribute<SkillHandlerAttribute>();
            if (attr == null)
                continue;
            var skill = attr.SkillType;
            if (skill == CharacterSkill.None)
                continue; //you can disable a handler by setting it's skill to None
            handler.SkillClassification = attr.SkillClassification;
            handler.Skill = attr.SkillType;

            handlers[(int)skill] = handler;
            skillAttributes[(int)skill] = attr;
        }

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<MonsterSkillHandlerAttribute>() != null))
        {
            var handler = (SkillHandlerBase)Activator.CreateInstance(type)!;
            var attr = type.GetCustomAttribute<MonsterSkillHandlerAttribute>();
            if (attr == null) continue;
            var skill = attr.SkillType;
            if (skill == CharacterSkill.None)
                continue; //you can disable a handler by setting it's skill to None
            handler.SkillClassification = attr.SkillClassification;
            handler.Skill = attr.SkillType;

            //we use the same handler as players
            //handlers[(int)skill] = handler;
            monsterSkillAttributes[(int)skill] = attr;
        }

        for (var i = 0; i < count; i++)
        {
            if (handlers[i] == null)
            {
                handlers[i] = new SkillHandlerGenericCast()
                {
                    Skill = (CharacterSkill)i,
                    SkillClassification = SkillClass.None
                };
                skillAttributes[i] = new SkillHandlerAttribute((CharacterSkill)i, SkillClass.None, SkillTarget.Self);
            }

            if (monsterSkillAttributes[i] == null!)
                monsterSkillAttributes[i] = new MonsterSkillHandlerAttribute(skillAttributes[i].SkillType, skillAttributes[i].SkillClassification, skillAttributes[i].SkillTarget);
        }
    }

    public static bool ShouldSkillCostSp(CharacterSkill skill, CombatEntity src)
    {
        var handler = handlers[(int)skill];
        if (handler != null)
            return handler.ShouldSkillCostSp(src);

        return true;
    }

    public static SkillValidationResult ValidateTarget(SkillCastInfo info, CombatEntity src, bool isIndirect = false, bool isItemSource = false)
    {
        CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();

        var handler = handlers[(int)info.Skill];
        if (handler != null)
            return handler.ValidateTarget(src, target, info.TargetedPosition, info.Level, isIndirect, isItemSource);

        return SkillValidationResult.Failure;
    }

    public static SkillValidationResult ValidateTarget(CharacterSkill skill, CombatEntity src, CombatEntity? target, Position pos, int lvl)
    {
        var handler = handlers[(int)skill];
        if (handler != null)
            return handler.ValidateTarget(src, target, pos, lvl, false, false);

        return SkillValidationResult.Failure;
    }

    public static float GetSkillCastTime(CharacterSkill skill, CombatEntity src, CombatEntity? target, int level)
    {
        var handler = handlers[(int)skill];
        if (handler != null)
        {
            var castMultiplier = 1f;
            if (src.Character.Type == CharacterType.Player)
            {
                var dex = src.GetEffectiveStat(CharacterStat.Dex);
                if (dex > 100)
                    dex = 100 + (dex - 100) * 2; //double effectiveness above 100
                castMultiplier = 1 * MathHelper.PowScaleDown(dex);

                //this should be handled some other way...
                if (src.StatusContainer?.TryGetExistingStatus(CharacterStatusEffect.Suffragium, out var suff) ?? false)
                {
                    castMultiplier *= (1 - 0.15f * suff.Value1);
                    src.StatusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Suffragium);
                }
            }

            return handler.GetCastTime(src, target, level) * castMultiplier;
        }

        return 0f;
    }

    public static int GetSkillRange(CombatEntity src, CharacterSkill skill, int level)
    {
        var handler = handlers[(int)skill];
        if (handler != null)
        {
            var range = handler.GetSkillRange(src, level);
            if (range < 0)
                return src.GetEffectiveStat(CharacterStat.Range);

            if (src.HasBodyState(BodyStateFlags.Blind))
                return int.Min(range, ServerConfig.MaxAttackRangeWhileBlind);

            return range;
        }

        return src.GetEffectiveStat(CharacterStat.Range);
    }

    public static bool ExecuteSkill(SkillCastInfo info, CombatEntity src)
    {
        if (!src.Character.IsActive || src.Character.Map == null)
            return false;

        CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
        var handler = handlers[(int)info.Skill];
        if ((info.Flags & SkillCastFlags.NoEffect) > 0)
            handler = handlers[(int)CharacterSkill.NoEffectAttack];
            
        if (handler != null)
        {
            var isIndirect = info.IsIndirect || info.ItemSource > 0;

            if (skillAttributes[(int)info.Skill].SkillTarget != SkillTarget.Passive)
            {
                if (!handler.PreProcessValidation(src, target, info.TargetedPosition, info.Level, isIndirect, info.ItemSource > 0))
                    return false;

                if (target != null && target.Character.Type == CharacterType.Player
                                   && skillAttributes[(int)info.Skill].SkillClassification == SkillClass.Magic
                                   && target.GetStat(CharacterStat.MagicImmunity) > 0)
                {
                    if(src.Character.Type == CharacterType.Player)
                        CommandBuilder.SkillFailed(src.Player, SkillValidationResult.TargetImmuneToEffect);
                    return false;
                }

                if (info.Skill != CharacterSkill.Cloaking && info.Skill != CharacterSkill.Hiding)
                    src.UpdateHidingStateAfterAttack();

                handler.Process(src, target, info.TargetedPosition, info.Level, isIndirect, info.ItemSource > 0);
                return true;
            }
        }

        return false;
    }

}
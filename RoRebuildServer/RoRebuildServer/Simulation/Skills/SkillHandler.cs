using System.Reflection;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Skills.SkillHandlers;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills
{
    public static class SkillHandler
    {
        private static SkillHandlerBase?[] handlers;
        private static SkillHandlerAttribute[] skillAttributes;
        private static MonsterSkillHandlerAttribute[] monsterSkillAttributes;

        public static SkillHandlerAttribute GetSkillAttributes(CharacterSkill skill) => skillAttributes[(int)skill];
        public static MonsterSkillHandlerAttribute GetMonsterSkillAttributes(CharacterSkill skill) => monsterSkillAttributes[(int)skill];

        public static void ApplyPassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.ApplyPassiveEffects(owner, level);
        public static void RemovePassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.RemovePassiveEffects(owner, level);

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

        public static SkillValidationResult ValidateTarget(SkillCastInfo info, CombatEntity src)
        {
            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();

            var handler = handlers[(int)info.Skill];
            if (handler != null)
                return handler.ValidateTarget(src, target, info.TargetedPosition, info.Level);

            return SkillValidationResult.Failure;
        }

        public static SkillValidationResult ValidateTarget(CharacterSkill skill, CombatEntity src, CombatEntity? target, Position pos, int lvl)
        {
            var handler = handlers[(int)skill];
            if (handler != null)
                return handler.ValidateTarget(src, target, pos, lvl);

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
                return range < 0 ? src.GetStat(CharacterStat.Range) : range;
            }

            return src.GetStat(CharacterStat.Range);
        }

        public static bool ExecuteSkill(SkillCastInfo info, CombatEntity src)
        {
            if (!src.Character.IsActive || src.Character.Map == null)
                return false;

            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
            var handler = handlers[(int)info.Skill];
            if (handler != null)
            {
                if (skillAttributes[(int)info.Skill].SkillTarget != SkillTarget.Passive)
                {
                    if (!handler.PreProcessValidation(src, target, info.TargetedPosition, info.Level, info.IsIndirect))
                        return false;
                    handler.Process(src, target, info.TargetedPosition, info.Level, info.IsIndirect);
                    return true;
                }
            }

            return false;
        }

    }
}

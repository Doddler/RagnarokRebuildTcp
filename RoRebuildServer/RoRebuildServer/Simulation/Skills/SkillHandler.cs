using System.Reflection;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Skills.SkillHandlers;

namespace RoRebuildServer.Simulation.Skills
{
    public static class SkillHandler
    {
        private static SkillHandlerBase?[] handlers;
        private static SkillHandlerAttribute[] skillAttributes;

        public static SkillHandlerAttribute GetSkillAttributes(CharacterSkill skill) => skillAttributes[(int)skill];

        public static void ApplyPassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.ApplyPassiveEffects(owner, level);
        public static void RemovePassiveEffects(CharacterSkill skill, CombatEntity owner, int level) => handlers[(int)skill]?.RemovePassiveEffects(owner, level);

        static SkillHandler()
        {
            var count = Enum.GetNames(typeof(CharacterSkill)).Length;
            handlers = new SkillHandlerBase[count];
            skillAttributes = new SkillHandlerAttribute[count];
            
            foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<SkillHandlerAttribute>() != null))
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
            }
        }

        public static SkillValidationResult ValidateTarget(SkillCastInfo info, CombatEntity src)
        {
            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
            
            var handler = handlers[(int)info.Skill];
            if (handler != null)
                return handler.ValidateTarget(src, target, info.TargetedPosition);

            return SkillValidationResult.Failure;
        }
        
        public static float GetSkillCastTime(CharacterSkill skill, CombatEntity src, CombatEntity? target, int level)
        {
            var handler = handlers[(int)skill];
            if (handler != null)
            {
                var castMultiplier = 1f;
                if (src.Character.Type == CharacterType.Player)
                    castMultiplier = 1 - (src.GetStat(CharacterStat.Level) * 0.0066f);
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

        public static void ExecuteSkill(SkillCastInfo info, CombatEntity src)
        {
            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
            var handler = handlers[(int)info.Skill];
            if (handler != null)
            {
                if (skillAttributes[(int)info.Skill].SkillTarget != SkillTarget.Passive)
                    handler.Process(src, target, info.TargetedPosition, info.Level, info.IsIndirect);
            }
        }
        
    }
}

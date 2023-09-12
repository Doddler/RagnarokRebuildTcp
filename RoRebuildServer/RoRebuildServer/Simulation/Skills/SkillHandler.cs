using System.Reflection;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills
{
    public static class SkillHandler
    {
        private static SkillHandlerBase?[] handlers;

        static SkillHandler()
        {
            var count = System.Enum.GetNames(typeof(CharacterSkill)).Length;
            handlers = new SkillHandlerBase[count];

            foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<SkillHandlerAttribute>() != null))
            {
                var handler = (SkillHandlerBase)Activator.CreateInstance(type)!;
                var attr = type.GetCustomAttribute<SkillHandlerAttribute>();
                var skill = attr.SkillType;

                handlers[(int)skill] = handler;
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
        
        public static float GetSkillCastTime(CharacterSkill skill, CombatEntity src, CombatEntity target, int level)
        {
            var handler = handlers[(int)skill];
            if (handler != null)
                return handler.GetCastTime(src, target, level);
            return 0f;
        }

        public static void ExecuteSkill(SkillCastInfo info, CombatEntity src)
        {
            CombatEntity? target = info.TargetEntity.GetIfAlive<CombatEntity>();
            var handler = handlers[(int)info.Skill];
            if (handler != null)
                handler.Process(src, target, info.TargetedPosition, info.Level);
        }
        
    }
}

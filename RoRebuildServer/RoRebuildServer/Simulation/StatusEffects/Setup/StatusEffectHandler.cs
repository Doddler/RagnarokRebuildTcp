using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Skills.SkillHandlers;
using System.Reflection;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public static class StatusEffectHandler
    {
        private static StatusEffectBase[] handlers;
        private static StatusEffectHandlerAttribute[] attributes;

        public static StatusClientVisibility GetStatusVisibility(CharacterStatusEffect status) => attributes[(int)status].VisibilityMode;

        static StatusEffectHandler()
        {
            var count = Enum.GetNames(typeof(CharacterStatusEffect)).Length;
            handlers = new StatusEffectBase[count];
            attributes = new StatusEffectHandlerAttribute[count];
            
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<StatusEffectHandlerAttribute>() != null))
            {
                var handler = (StatusEffectBase)Activator.CreateInstance(type)!;
                var attr = type.GetCustomAttribute<StatusEffectHandlerAttribute>();
                var status = attr.StatusType;
                if (status == CharacterStatusEffect.None)
                    continue; //you can disable a handler by setting it's skill to None

                handlers[(int)status] = handler;
                attributes[(int)status] = attr;
            }

            for (var i = 0; i < count; i++)
            {
                if (handlers[i] == null)
                {
                    handlers[i] = new StatusEffectBase();
                    attributes[i] = new StatusEffectHandlerAttribute((CharacterStatusEffect)i, StatusClientVisibility.None);
                }
            }
        }

        public static bool TestApplication(CharacterStatusEffect type, CombatEntity ch, float testValue) => handlers[(int)type].TestApplication(ch, testValue);
        public static void OnApply(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state) => handlers[(int)type].OnApply(ch, ref state);
        public static void OnExpiration(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state) => handlers[(int)type].OnExpiration(ch, ref state);
        public static void OnAttack(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state, ref DamageInfo info) => handlers[(int)type].OnAttack(ch, ref state, ref info);
        public static void OnTakeDamage(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state, ref DamageInfo info) => handlers[(int)type].OnTakeDamage(ch, ref state, ref info);
    }
}

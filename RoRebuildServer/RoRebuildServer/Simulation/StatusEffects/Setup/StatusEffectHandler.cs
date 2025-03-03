using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Skills.SkillHandlers;
using System.Reflection;
using RebuildSharedData.Data;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public static class StatusEffectHandler
    {
        private static StatusEffectBase[] handlers;
        private static StatusEffectHandlerAttribute[] attributes;

        private static StatusUpdateMode[] updateModes;

        public static StatusUpdateMode GetUpdateMode(CharacterStatusEffect status) => updateModes[(int)status];
        public static StatusClientVisibility GetStatusVisibility(CharacterStatusEffect status) => attributes[(int)status].VisibilityMode;
        public static string GetShareGroup(CharacterStatusEffect status) => attributes[(int)status].ShareGroup;
        public static bool HasFlag(CharacterStatusEffect status, StatusEffectFlags flag) => attributes[(int)status].Flags.HasFlag(flag);

        static StatusEffectHandler()
        {
            var count = Enum.GetNames(typeof(CharacterStatusEffect)).Length;
            handlers = new StatusEffectBase[count];
            attributes = new StatusEffectHandlerAttribute[count];
            updateModes = new StatusUpdateMode[count];
            
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsClass && t.GetCustomAttributes<StatusEffectHandlerAttribute>().Any()))
            {
                var handler = (StatusEffectBase)Activator.CreateInstance(type)!;
                var attrTypes = type.GetCustomAttributes<StatusEffectHandlerAttribute>();
                foreach (var attr in attrTypes)
                {
                    var status = attr.StatusType;
                    if (status == CharacterStatusEffect.None)
                        continue; //you can disable a handler by setting it's skill to None

                    handlers[(int)status] = handler;
                    attributes[(int)status] = attr;
                    updateModes[(int)status] = handler.UpdateMode;
                }
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
        public static void OnRestore(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state) => handlers[(int)type].OnRestore(ch, ref state);
        public static void OnExpiration(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state) => handlers[(int)type].OnExpiration(ch, ref state);
        public static StatusUpdateResult OnUpdateTick(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state) => handlers[(int)type].OnUpdateTick(ch, ref state);
        public static StatusUpdateResult OnAttack(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state, ref DamageInfo info) => handlers[(int)type].OnAttack(ch, ref state, ref info);
        public static StatusUpdateResult OnTakeDamage(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state, ref DamageInfo info) => handlers[(int)type].OnTakeDamage(ch, ref state, ref info);
        public static StatusUpdateResult OnCalculateDamageTaken(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state, ref AttackRequest req, ref DamageInfo info) => handlers[(int)type].OnCalculateDamageTaken(ch, ref state, ref req, ref info);
        public static StatusUpdateResult OnChangeEquipment(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state) => handlers[(int)type].OnChangeEquipment(ch, ref state);
        public static StatusUpdateResult OnMove(CharacterStatusEffect type, CombatEntity ch, ref StatusEffectState state, Position src, Position dest) => handlers[(int)type].OnMove(ch, ref state, src, dest);
    }
}

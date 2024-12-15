using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Frozen, StatusClientVisibility.Everyone)]
    public class StatusFreeze : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage;
        
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var defDown = ch.GetEffectiveStat(CharacterStat.Def) / 2;
            var mdefUp = ch.GetEffectiveStat(CharacterStat.MDef) / 4;

            state.Value1 = defDown;
            state.Value2 = mdefUp;

            ch.AddStat(CharacterStat.AddDefPercent, -50);
            ch.AddStat(CharacterStat.AddMDefPercent, 25);
            ch.SetStat(CharacterStat.OverrideElement, (int)CharacterElement.Water1);

            ch.AddDisabledState();
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddDefPercent, -50);
            ch.SubStat(CharacterStat.AddMDefPercent, 25);
            ch.SetStat(CharacterStat.OverrideElement, (int)CharacterElement.None);

            ch.SubDisabledState();
        }

        public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
        {
            if (info.Result != AttackResult.Miss)
                return StatusUpdateResult.EndStatus;
            return StatusUpdateResult.Continue;
        }
    }
}

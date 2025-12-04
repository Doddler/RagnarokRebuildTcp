using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Curse, StatusClientVisibility.Everyone)]
    public class StatusCurse : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            //ignore luk as that's inside GetEffective for luck
            ch.SetBodyState(BodyStateFlags.Curse);
            ch.AddStat(CharacterStat.AddAttackPercent, -25);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.RemoveBodyState(BodyStateFlags.Curse);
            ch.SubStat(CharacterStat.AddAttackPercent, -25);
        }
    }
}
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Frozen, StatusClientVisibility.Everyone)]
    public class StatusFreeze : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnCalculateDamageTaken | StatusUpdateMode.OnTakeDamage;

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddDefPercent, -50);
            ch.AddStat(CharacterStat.AddMDefPercent, 25);
            ch.AddStat(CharacterStat.AddFlee, -999);
            ch.SetStat(CharacterStat.OverrideElement, (int)CharacterElement.Water1);

            ch.SetBodyState(BodyStateFlags.Frozen);
            ch.Character.StopMovingImmediately();
            ch.AddDisabledState();
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddDefPercent, -50);
            ch.SubStat(CharacterStat.AddMDefPercent, 25);
            ch.SubStat(CharacterStat.AddFlee, -999);
            ch.SetStat(CharacterStat.OverrideElement, (int)CharacterElement.None);

            ch.RemoveBodyState(BodyStateFlags.Frozen);
            ch.SubDisabledState();
        }

        public override StatusUpdateResult OnCalculateDamage(CombatEntity ch, ref StatusEffectState state, ref AttackRequest req,
            ref DamageInfo info)
        {
            if (state.Value4 == 0 && info.IsDamageResult && info.Damage > 0 && req.Element != AttackElement.Water)
                state.Value4 = 1;

            return StatusUpdateResult.Continue;
        }

        public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
        {
            if (info.IsDamageResult && state.Value4 == 1)
                return StatusUpdateResult.EndStatus;
            return StatusUpdateResult.Continue;
        }
    }
}
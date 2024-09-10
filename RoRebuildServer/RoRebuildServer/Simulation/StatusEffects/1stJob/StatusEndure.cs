using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.Endure, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class StatusEndure : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage;

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.Result != AttackResult.NormalDamage && info.Result != AttackResult.CriticalDamage)
            return StatusUpdateResult.Continue;
        info.Flags |= DamageApplicationFlags.NoHitLock;
        state.Value1--;
        if (state.Value1 <= 0)
            return StatusUpdateResult.EndStatus;
        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AddMDef, state.Value2);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddMDef, state.Value2);
    }
}
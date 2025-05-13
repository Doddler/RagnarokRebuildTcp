using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.MagicalAttack, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
public class MagicalAttackStatus : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnPreCalculateDamageDealt;

    public override StatusUpdateResult OnPreCalculateDamage(CombatEntity ch, CombatEntity? target, ref StatusEffectState state, ref AttackRequest req)
    {
        if (req.SkillSource == CharacterSkill.None && (req.Flags & AttackFlags.Physical) > 0)
        {
            req.Flags = (req.Flags & ~AttackFlags.Physical) | AttackFlags.Magical | AttackFlags.IgnoreEvasion;
            (req.MinAtk, req.MaxAtk) = ch.CalculateAttackPowerRange(true);
        }

        return StatusUpdateResult.Continue;
    }
}
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.MagnumBreak, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
public class StatusMagnumBreak : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

    //Adds damage equal to [10 + 1 * SkillLevel]% of a full attack in fire damage.
    //Ignores sub def as it's already applied to the base attack.
    //If the base attack crits, the bonus damage also crits.
    public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult && info.Flags.HasFlag(DamageApplicationFlags.PhysicalDamage))
        {
            if (!info.Target.TryGet<CombatEntity>(out var target))
                return StatusUpdateResult.Continue;

            var attack = new AttackRequest(CharacterSkill.MagnumBreak, 1f, 1, 
                AttackFlags.Physical | AttackFlags.IgnoreEvasion | AttackFlags.NoTriggers, AttackElement.Fire);

            if (info.Result == AttackResult.CriticalDamage)
                attack.Flags |= AttackFlags.GuaranteeCrit;
            else
                attack.Flags |= AttackFlags.IgnoreSubDefense;

            var res = ch.CalculateCombatResult(target, attack);

            if (res.Damage > 0)
            {
                res.Time = info.Time + 0.1f;
                if (ch.Character.Type == CharacterType.Player && ch.Player.Equipment.IsDualWielding)
                    res.Time += 0.1f; //dual-wielding offhand triggers at 0.1s, and double attack at 0.3s, so we'll slot in at 0.2s in this case

                res.IsIndirect = true;
                res.Damage = res.Damage * (10 + state.Value1) / 100;
                target.QueueDamage(res);
                CommandBuilder.AttackAutoVis(ch.Character, target.Character, res, false);
            }
                //info.Damage += res.Damage * (10 + state.Value1) / 100;
        }

        return StatusUpdateResult.Continue;
    }
}

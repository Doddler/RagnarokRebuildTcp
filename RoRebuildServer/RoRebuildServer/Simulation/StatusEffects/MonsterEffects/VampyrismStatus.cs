using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.Vampyrism, StatusClientVisibility.None)]
public class VampyrismStatus : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

    public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.Damage > 0)
        {
            var res2 = DamageInfo.SupportSkillResult(ch.Entity, ch.Entity, CharacterSkill.BloodDrain);
            res2.Damage = -info.Damage/2;
            res2.Time = Time.ElapsedTimeFloat + 1f;
            res2.Result = AttackResult.Heal;

            ch.QueueDamage(res2);
        }

        return StatusUpdateResult.Continue;
    }
}
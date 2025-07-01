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
        if (info.AttackSkill == CharacterSkill.None)
            info.AttackSkill = CharacterSkill.BloodDrain;

        if (info.Damage > 0)
        {
            var res2 = DamageInfo.SupportSkillResult(ch.Entity, ch.Entity, CharacterSkill.BloodDrain);
            res2.Damage = -info.Damage;
            res2.Time = Time.ElapsedTimeFloat + res2.AttackMotionTime + 0.8f;
            res2.Result = AttackResult.Heal;

            ch.QueueDamage(res2);
        }

        return StatusUpdateResult.Continue;
    }
}
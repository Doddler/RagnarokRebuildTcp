using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.EarthSpike)]
    public class EarthSpikeHandler : SkillHandlerBase
    {
        public override bool DoesAttackTakeWeaponSound => false;

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Earth, attack.HitCount);
        }
        
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Earth));

            if (target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            if(src != null && attack.Target != null)
                EarthSpikeEffect.Create(attack.Target.transform.position, attack.DamageTiming - 0.2f);
        }
    }
}
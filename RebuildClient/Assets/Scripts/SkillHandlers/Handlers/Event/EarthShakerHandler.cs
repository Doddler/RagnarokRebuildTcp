using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills.Custom;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers.Event
{
    [SkillHandler(CharacterSkill.EarthShaker)]
    public class EarthShakerHandler : SkillHandlerBase
    {
        public override bool DoesAttackTakeWeaponSound => false;

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Earth, attack.HitCount);
        }

        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            //HoldStandbyMotionForCast(src, castTime);
             src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Earth));
        }

        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            EarthShakerEffect.Create(src.transform.position, attack.TargetAoE.ToWorldPosition());
        }
    }
}
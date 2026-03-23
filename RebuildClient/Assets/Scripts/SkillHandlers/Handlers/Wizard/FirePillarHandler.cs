using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.FirePillar)]
    public class FirePillarHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendHitEffect(null, attack.MotionTime, 0, attack.HitCount);
        }
        
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 1;

        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Fire));

            if (target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            AudioManager.Instance.OneShotSoundEffect(-1, "wizard_fire_pillar_a.ogg", attack.TargetAoE.ToWorldPosition(), 1f);
            base.ExecuteSkillGroundTargeted(src, ref attack);
        }
    }
}
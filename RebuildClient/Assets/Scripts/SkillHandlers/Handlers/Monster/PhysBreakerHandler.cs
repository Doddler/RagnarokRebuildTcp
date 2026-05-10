using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.PhysBreaker)]
    public class PhysBreakerHandler : SkillHandlerBase
    {

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendElementalHitEffect(attack.Src, attack.DamageTiming, AttackElement.Dark, attack.HitCount);
        }
        
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Dark));
            target?.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }
        
        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            if (attack.Target == null || attack.Result != AttackResult.NormalDamage)
                return;
            
            var effect = RoSpriteEffect.AttachSprite(attack.Target, "Assets/Sprites/Effects/darkbreath.spr", 2f, 1f, RoSpriteEffectFlags.EndWithAnimation);
            effect.SetDurationByFrames(90);
            src.AttachEffect(effect);

            effect.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
        }
    }
}
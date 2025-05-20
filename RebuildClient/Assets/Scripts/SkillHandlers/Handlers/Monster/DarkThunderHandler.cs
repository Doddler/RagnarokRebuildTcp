using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.DarkThunder)]
    public class DarkThunderHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            var len = Mathf.Clamp(0.2f + attack.HitCount * 0.2f, 0.6f, 4f);
            JupitelHitEffect.Attach(target, len,  attack.DamageTiming);
        }

        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Dark));

            if (target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void InterruptSkillCasting(ServerControllable src)
        {
            src.EndEffectOfType(EffectType.CastEffect);
        }

        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            if (src != null && attack.Target != null)
                JupitelBallEffect.Attach(src, attack.Target, attack.MotionTime * 0.65f);
            //     IceArrow.Create(src, attack.Target, attack.SkillLevel); //don't attach to the entity so the effect stays if they get removed
        }
    }
}
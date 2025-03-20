using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.MagnumBreak)]
    public class MagnumBreakHandler : SkillHandlerBase
    {
        // public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        // {
        //     attack.Target?.Messages.SendHitEffect(attack.Src, attack.MotionTime, 2);
        // }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion();
            var effect = MagnumBreakEffect.Attach(src, attack.MotionTime);
            if (attack.SkillLevel > 10)
                effect.gameObject.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_magnumbreak.ogg", src.gameObject);
        }
    }
}
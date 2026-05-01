using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Assassin
{
    [SkillHandler(CharacterSkill.PoisonReact)]
    public class PoisonReactHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target?.Messages.SendHitEffect(attack.Src, attack.MotionTime, 2);
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (!attack.IsIndirect)
            {
                src.PerformSkillMotion();
                CameraFollower.Instance.AttachEffectToEntity("PoisonReactSelf", src.gameObject);
            }
            else
                DefaultSkillCastEffect.Create(src);
        }
    }
}
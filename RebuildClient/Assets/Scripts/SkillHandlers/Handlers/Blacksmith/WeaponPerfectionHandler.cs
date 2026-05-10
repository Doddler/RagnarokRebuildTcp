using Assets.Scripts.Effects.EffectHandlers.Skills.Blacksmith;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Blacksmith
{
    [SkillHandler(CharacterSkill.WeaponPerfection)]
    public class WeaponPerfectionHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (src == null)
                return;
            
            CameraFollower.Instance.AttachEffectToEntity("WeaponPerfection", src.gameObject, src.Id);
            //WeaponPerfectionEffect.Create(src);
            if(!attack.IsIndirect)
                src.PerformSkillMotion(); //don't set color for direct casts, that'll be applied via StatusEffectApplicator
            else
            {
                if (src.SpriteAnimator != null)
                {
                    src.SpriteAnimator.TransitionColor = new Color(0.98f, 0.58f, 0.58f);
                    src.SpriteAnimator.ColorTransitionSpeed = 0.5f;
                }
            }
                
        }
    }
}
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.StatusEffects;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Invisible)]
    public class InvisibleHandler : SkillHandlerBase
    {
        public override bool ShowSkillAttackName => false;

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (!src.SpriteAnimator.IsHidden)
            {
                //DefaultSkillCastEffect.Create(src);
                //HideEffect.AttachHideEffect(src.gameObject);
            }

            src.PerformSkillMotion();
            //CameraFollower.Instance.AttachEffectToEntity("Hiding", src.gameObject, src.Id);
            //HideEffect.AttachHideEffect(src);
        }
    }
}
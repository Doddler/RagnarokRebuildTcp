using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Mammonite)]
    public class MammoniteHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            
            CameraFollower.Instance.AttachEffectToEntity("Mammonite", target.gameObject, src.Id);
        }
    }
}
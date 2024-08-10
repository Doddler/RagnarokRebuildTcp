using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Mammonite)]
    public class MammoniteHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl, int damage)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject, 1.2f);
            
            CameraFollower.Instance.AttachEffectToEntity("Mammonite", target.gameObject, src.Id);
            CameraFollower.Instance.AttachEffectToEntity("MammoniteCoins", target.gameObject, src.Id);
        }
    }
}
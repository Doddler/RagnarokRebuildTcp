using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Envenom)]
    public class EnvenomHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
            if(attack.Damage > 0)
                attack.Target?.Messages.SendHitEffect(src, attack.MotionTime, 2);
        }
    }
}
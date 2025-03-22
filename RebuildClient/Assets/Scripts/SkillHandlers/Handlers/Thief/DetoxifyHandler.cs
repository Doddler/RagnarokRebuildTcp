using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Detoxify)]
    public class DetoxifyHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();

            if (attack.Target != null)
            {
                AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_detoxication.ogg", attack.Target.gameObject);
                DetoxEffect.LaunchEffect(attack.Target);
            }
        }
    }
}
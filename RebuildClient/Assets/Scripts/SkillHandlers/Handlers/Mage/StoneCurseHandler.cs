using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.StoneCurse)]
    public class StoneCurseHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Earth));
            target?.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //src.PerformSkillMotion();
            if (src != null && attack.Target != null && attack.Result == AttackResult.Success)
            {
                src.PerformSkillMotion();
                CameraFollower.Instance.AttachEffectToEntity("StoneCurse", attack.Target.gameObject, src.Id);
                AudioManager.Instance.AttachSoundToEntity(src.Id, "_stonecurse.ogg", attack.Target.gameObject);
            }
        }
    }
}
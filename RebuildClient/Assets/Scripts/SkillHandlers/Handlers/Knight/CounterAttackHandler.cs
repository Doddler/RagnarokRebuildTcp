using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.CounterAttack)]
    public class CounterAttackHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldAttackMotionForCast(src, 4);
            //src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Water));
        }

        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.LookAtOrDefault(attack.Target);
            src.PerformBasicAttackMotion();
            CameraFollower.Instance.AttachEffectToEntity("CounterAttack", src.gameObject, src.Id);
            AudioManager.Instance.OneShotSoundEffect(src.Id, "knight_autocounter.ogg", src.transform.position, 1f);
        }
    }
}
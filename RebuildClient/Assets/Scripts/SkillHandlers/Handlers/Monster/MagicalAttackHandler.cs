using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.MagicalAttack)]
    public class MagicalAttackHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (attack.Target != null)
            {
                src.LookAt(attack.Target.transform.position);
                CameraFollower.Instance.AttachEffectToEntity("MagicalAttack", src.gameObject, src.Id);
            }
            
            src.PerformBasicAttackMotion(CharacterSkill.MagicalAttack);
        }
    }
}
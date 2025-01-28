using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.FireAttack)]
    public class FireAttackHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion(CharacterSkill.WindAttack);
            if(attack.Damage > 0)
                attack.Target?.Messages.SendElementalHitEffect(src, attack.MotionTime, AttackElement.Fire);
        }
    }
}
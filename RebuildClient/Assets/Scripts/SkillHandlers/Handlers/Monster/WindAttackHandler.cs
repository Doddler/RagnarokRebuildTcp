using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.WindAttack)]
    public class WindAttackHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion(CharacterSkill.WindAttack);
            if(attack.Damage > 0)
                attack.Target?.Messages.SendElementalHitEffect(src, attack.MotionTime, AttackElement.Wind);
        }
    }
}
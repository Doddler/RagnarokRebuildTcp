using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers.Assassin
{
    [SkillHandler(CharacterSkill.EnchantPoison)]
    public class EnchantPoisonHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            var skillMotion = src.PerformSkillMotion();
            
            if (attack.Target != null)
            {
                EnchantPoisonEffect.LaunchEffect(attack.Target);
                if(skillMotion && src != attack.Target)
                    src.LookAt(attack.Target.transform.position);
            }
        }
    }
}
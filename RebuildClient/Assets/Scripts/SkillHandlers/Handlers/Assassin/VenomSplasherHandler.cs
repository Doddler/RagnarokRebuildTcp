using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers.Assassin
{
    [SkillHandler(CharacterSkill.VenomSplasher)]
    public class VenomSplasherHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (attack.Target != null)
            {
                EnchantPoisonEffect.LaunchEffect(attack.Target);
                if(src != attack.Target)
                    src.LookAt(attack.Target.transform.position);
            }
            
            src.PerformBasicAttackMotion();
        }
    }
}
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Provoke)]
    public class ProvokeHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (attack.Target != null)
            {
                ProvokeEffect.Provoke(attack.Target);
                src.LookAt(attack.Target.transform.position);
            }
            
            src.PerformSkillMotion();
        }
    }
}
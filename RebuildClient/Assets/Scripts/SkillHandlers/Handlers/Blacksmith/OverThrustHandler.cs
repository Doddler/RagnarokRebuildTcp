using Assets.Scripts.Effects.EffectHandlers.Skills.Blacksmith;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers.Blacksmith
{
    [SkillHandler(CharacterSkill.PowerThrust)]
    public class OverThrustHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            OverThrustEffect.Create(src);
            if(!attack.IsIndirect)
                src.PerformSkillMotion();
        }
    }
}
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.AdrenalineRush)]
    public class AdrenalineRushHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            AdrenalineRushEffect.Create(src);
            src.PerformSkillMotion();
        }
    }
}
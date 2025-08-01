using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers.Event
{
    [SkillHandler(CharacterSkill.RollingThunder)]
    public class RollingThunderHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Wind));
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
        }
    }
}
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.MeteorStorm)]
    public class MeteorStormHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendHitEffect(null, attack.MotionTime, 0, attack.HitCount);
            //target.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Neutral, attack.HitCount);
        }
        
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            var size = lvl <= 10 ? 6 : 9;
            HoldStandbyMotionForCast(src, castTime);
            CreateGroundTargetCircle(src, target, size, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Fire));
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformSkillMotion();
        }
    }
}
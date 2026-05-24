using Assets.Scripts.Effects.EffectHandlers.Skills.Hunter;
using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Hunter
{
    [SkillHandler(CharacterSkill.Detect)]
    public class DetectHandler : SkillHandlerBase
    {
        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            var worldTarget = attack.TargetAoE.ToWorldPosition();
            src.StopImmediate(Vector2Int.zero, false);
            if (attack.TargetAoE.x > 0 && attack.TargetAoE.y > 0)
            {
                src.LookAt(worldTarget);
                DetectEffect.Create(src, attack.TargetAoE);
            }
            
            src.PerformSkillMotion();
            
            var follower = src.FollowerObject;
            if (follower == null)
                return;
            var bird = follower.GetComponent<BirdFollower>();
            if (bird != null)
                bird.LaunchAttackToPosition(worldTarget);
        }
    }
}
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Hunter
{
    [SkillHandler(CharacterSkill.RemoveTrap)]
    public class RemoveTrapHandler : SkillHandlerBase
    {
        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.StopImmediate(Vector2Int.zero, false);
            if(attack.Target != null)
                src.LookAtOrDefault(attack.Target);
            else if (attack.TargetAoE.x > 0 && attack.TargetAoE.y > 0)
                src.LookAt(attack.TargetAoE.ToWorldPosition());
            src.SpriteAnimator.ChangeMotion(SpriteMotion.PickUp);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.StopImmediate(Vector2Int.zero, false);
            src.LookAtOrDefault(attack.Target);
            src.SpriteAnimator.ChangeMotion(SpriteMotion.PickUp);
        }
    }
}
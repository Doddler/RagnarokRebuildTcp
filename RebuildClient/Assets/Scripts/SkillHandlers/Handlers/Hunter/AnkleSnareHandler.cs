using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Hunter
{
    [SkillHandler(CharacterSkill.AnkleSnare)]
    public class AnkleSnareHandler : SkillHandlerBase
    {
        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.LookAt(attack.TargetAoE.ToWorldPosition());
            if (src.CharacterType == CharacterType.Player && src.SpriteAnimator != null)
            {
                src.StopImmediate(Vector2Int.zero, false);
                src.SpriteAnimator.ChangeMotion(SpriteMotion.PickUp);
                src.SpriteAnimator.State = SpriteState.Idle;
            }
            // else
            //     src.PerformSkillMotion();
        }
    }
}
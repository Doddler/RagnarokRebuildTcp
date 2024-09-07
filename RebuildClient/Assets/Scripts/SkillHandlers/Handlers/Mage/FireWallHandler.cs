using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.FireWall, true)]
    public class FireWallHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            if (src.SpriteAnimator.State != SpriteState.Dead && src.SpriteAnimator.State != SpriteState.Walking)
            {
                src.SpriteAnimator.State = SpriteState.Standby;
                src.SpriteAnimator.ChangeMotion(SpriteMotion.Standby);
                src.SpriteAnimator.PauseAnimation(castTime);
            }

            src.AttachEffect(CastEffect.Create(castTime, "ring_red", src.gameObject));
            
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            if(target != Vector2Int.zero)
                CastTargetCircle.Create(src.IsAlly, targetCell, 1, castTime);
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, Vector2Int target, int lvl)
        {
            src.PerformBasicAttackMotion();
        }

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            Debug.Log("OnHitEffect Firewall");
        }
    }
}
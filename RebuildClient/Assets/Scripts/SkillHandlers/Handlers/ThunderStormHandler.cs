using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.ThunderStorm)]
    public class ThunderStormHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            if (src.SpriteAnimator.State != SpriteState.Dead && src.SpriteAnimator.State != SpriteState.Walking)
            {
                src.SpriteAnimator.State = SpriteState.Standby;
                src.SpriteAnimator.ChangeMotion(SpriteMotion.Standby);
                src.SpriteAnimator.PauseAnimation(castTime);
            }

            src.AttachEffect(CastEffect.Create(castTime, "ring_yellow", src.gameObject));
            
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            CastTargetCircle.Create(src.IsAlly, targetCell, 3, castTime);
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, Vector2Int target, int lvl)
        {
            //DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
        }
    }
}
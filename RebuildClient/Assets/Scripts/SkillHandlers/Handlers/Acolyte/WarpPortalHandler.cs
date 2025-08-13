using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.WarpPortal)]
    public class WarpPortalHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 1;

        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Holy));
            
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            if(target != Vector2Int.zero)
                CastTargetCircle.Create(src.IsAlly, targetCell, 1, castTime);
        }

        public override void InterruptSkillCasting(ServerControllable src)
        {
            src.EndEffectOfType(EffectType.CastHolyEffect);
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.SpriteAnimator.State = SpriteState.Idle;
            src.PerformSkillMotion();
        }
    }
}
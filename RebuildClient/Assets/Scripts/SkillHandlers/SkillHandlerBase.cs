using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public abstract class SkillHandlerBase
    {
        public virtual bool DoesAttackTakeWeaponSound => true;
        public virtual bool ShowSkillAttackName => true;
        public virtual int GetSkillAoESize(ServerControllable src, int lvl) => 5;
        public virtual void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime) {}

        public virtual void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            if (target != Vector2Int.zero)
            {
                var size = GetSkillAoESize(src, lvl);
                CastTargetCircle.Create(src.IsAlly, targetCell, size, castTime);
            }
        }

        public virtual void InterruptSkillCasting(ServerControllable src)
        {
            src.EndEffectOfType(EffectType.CastEffect);
            src.EndEffectOfType(EffectType.CastHolyEffect);
        }

        //public virtual void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ServerControllable target, int lvl, int damage = 0) {}
        public virtual void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion();
        }

        public virtual void ExecuteSkillGroundTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src.PerformSkillMotion();
        }

        public virtual void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            //target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 1, attack.HitCount);
        }

        protected void HoldStandbyMotionForCast(ServerControllable src, float castTime)
        {
            if (src.SpriteAnimator.State != SpriteState.Dead && src.SpriteAnimator.State != SpriteState.Walking)
            {
                src.SpriteAnimator.State = SpriteState.Standby;
                src.SpriteAnimator.ChangeMotion(SpriteMotion.Casting);
                src.SpriteAnimator.PauseAnimation(); //castTime
            }
        }

        protected void CreateGroundTargetCircle(ServerControllable src, Vector2Int target, int size, float castTime)
        {
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            if(target != Vector2Int.zero)
                CastTargetCircle.Create(src.IsAlly, targetCell, size, castTime);
        }

        public bool ExecuteWithoutSource = false;

    }
}
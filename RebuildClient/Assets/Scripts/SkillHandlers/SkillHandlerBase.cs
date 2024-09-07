using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public abstract class SkillHandlerBase
    {
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
        public virtual void InterruptSkillCasting(ServerControllable src) {}

        //public virtual void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ServerControllable target, int lvl, int damage = 0) {}
        public virtual void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack) {}
        public virtual void ExecuteSkillGroundTargeted([CanBeNull] ServerControllable src, Vector2Int target, int lvl) {}

        public virtual void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 1);
        }

        public bool ExecuteWithoutSource = false;

    }
}
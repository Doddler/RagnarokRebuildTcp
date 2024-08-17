using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public abstract class SkillHandlerBase
    {
        public virtual void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime) {}
        public virtual void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime) {}
        public virtual void InterruptSkillCasting(ServerControllable src) {}

        //public virtual void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ServerControllable target, int lvl, int damage = 0) {}
        public virtual void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack) {}
        public virtual void ExecuteSkillGroundTargeted([CanBeNull] ServerControllable src, Vector2Int target, int lvl) {}

        public bool ExecuteWithoutSource = false;

    }
}
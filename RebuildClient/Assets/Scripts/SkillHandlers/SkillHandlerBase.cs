using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public abstract class SkillHandlerBase
    {
        public virtual void StartSkillCasting(ServerControllable src, ServerControllable target, SkillType skillType, int lvl, float castTime) {}
        public virtual void StartSkillCasting(ServerControllable src, Vector2Int target, SkillType skillType, int lvl, float castTime) {}
        public virtual void InterruptSkillCasting(ServerControllable src) {}

        public virtual void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl) {}
        
    }
}
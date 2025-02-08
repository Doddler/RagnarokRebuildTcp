using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SafetyWall)]
    public class SafetyWallHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 1;
        
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Ghost));
            base.StartSkillCasting(src, target, lvl, castTime);
        }
    }
}
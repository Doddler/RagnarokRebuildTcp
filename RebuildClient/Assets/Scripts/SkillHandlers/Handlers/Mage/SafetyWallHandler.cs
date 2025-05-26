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
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Ghost));
            base.StartSkillCasting(src, target, lvl, castTime);
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if(attack.TargetAoE == attack.AttackerPos)
                src.SnapToTile(attack.AttackerPos, 0.1f, 0.1f); //if they cast it on themselves they should be fully in the wall
            base.ExecuteSkillGroundTargeted(src, ref attack);
        }
    }
}
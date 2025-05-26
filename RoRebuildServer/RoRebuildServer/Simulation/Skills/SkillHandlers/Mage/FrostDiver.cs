using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage
{
    [SkillHandler(CharacterSkill.FrostDiver, SkillClass.Magic)]
    public class FrostDiverHandler : SkillHandlerBase
    {
        public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.8f;

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (lvl < 0 || lvl > 10)
                lvl = 10;

            if (target == null || !target.IsValidTarget(source))
                return;

            var res = source.CalculateCombatResult(target, 1f + lvl * 0.1f, 1, AttackFlags.Magical, CharacterSkill.FrostDiver, AttackElement.Water);
            if (target.IsElementBaseType(CharacterElement.Undead1) && res.Damage > 0)
                res.Damage = res.Damage * (100 + 5 * lvl) / 100; //5% bonus against undead per level

            var dist = source.Character.WorldPosition.DistanceTo(target.Character.WorldPosition);
            var distTime = dist * 0.04f;
            res.Time = Time.ElapsedTimeFloat + 0.38f + distTime;
            res.AttackMotionTime = 0.38f;

            if (!isIndirect)
            {
                source.ApplyAfterCastDelay(1f);
                source.ApplyCooldownForSupportSkillAction();
            }

            source.ExecuteCombatResult(res, false);

            if(res.Damage > 0)
                source.TryFreezeTarget(target, 350 + lvl * 30, res.AttackMotionTime + distTime + 0.08f);

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.FrostDiver, lvl, res, isIndirect);
        }
    }
}

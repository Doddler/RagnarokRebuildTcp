using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer
{
    [SkillHandler(CharacterSkill.DoubleStrafe, SkillClass.Physical, SkillTarget.Enemy)]
    public class DoubleStrafeHandler : SkillHandlerBase
    {
        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl) =>
            ValidateTargetForAmmunitionWeapon(source, target, position, 12, AmmoType.Arrow);
        
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            lvl = lvl.Clamp(1, 10);

            if (target == null || !target.IsValidTarget(source))
                return;

            var res = source.CalculateCombatResult(target, 0.9f + lvl * 0.1f, 2, AttackFlags.Physical | AttackFlags.Ranged, CharacterSkill.DoubleStrafe);
            res.Time += source.Character.Position.DistanceTo(target.Character.Position) / ServerConfig.ArrowTravelTime;
            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);

            var ch = source.Character;

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.DoubleStrafe, lvl, res);
        }
    }
}

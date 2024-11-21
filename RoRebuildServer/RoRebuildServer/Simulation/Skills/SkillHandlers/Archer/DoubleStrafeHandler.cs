using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer
{
    [SkillHandler(CharacterSkill.DoubleStrafe, SkillClass.Physical, SkillTarget.Enemy)]
    public class DoubleStrafeHandler : SkillHandlerBase
    {
        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position)
        {
            if(source.Character.Type != CharacterType.Player)
                return base.ValidateTarget(source, target, position);
            if(source.Character.Player.WeaponClass == 12) //bows only!
                return base.ValidateTarget(source, target, position);

            return SkillValidationResult.IncorrectWeapon;
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            lvl = lvl.Clamp(1, 10);

            if (target == null || !target.IsValidTarget(source))
                return;

            var res = source.CalculateCombatResult(target, 0.9f + lvl * 0.1f, 2, AttackFlags.Physical, CharacterSkill.DoubleStrafe);
            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);

            var ch = source.Character;

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.DoubleStrafe, lvl, res);
        }
    }
}

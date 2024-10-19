using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.PoisonAttack, SkillClass.Physical)]
public class PoisonAttack : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, lvl, 1, AttackFlags.Physical, CharacterSkill.PoisonAttack, AttackElement.Poison);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.PoisonAttack, lvl, res);
    }
}
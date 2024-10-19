using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.CriticalAttack, SkillClass.Physical)]
public class CriticalAttack : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        //surely there's a better way to do it than this?
        var res = source.CalculateCombatResult(target, 0.7f + lvl * 0.3f, 1, AttackFlags.Physical | AttackFlags.GuaranteeCrit, CharacterSkill.CriticalAttack);
        
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.CriticalAttack, lvl, res);
    }
}
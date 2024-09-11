using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant;

[SkillHandler(CharacterSkill.Mammonite, SkillClass.Physical)]
public class MammoniteHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;
            
        var res = source.CalculateCombatResult(target, 1f + lvl * 0.5f, 1, AttackFlags.Physical, CharacterSkill.Mammonite);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Mammonite, lvl, res);
    }
}
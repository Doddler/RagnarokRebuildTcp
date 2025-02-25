using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.ComboAttack)]
public class ComboAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 8;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        var total = 0.8f + lvl * 0.6f;
        var perHit = total / (lvl + 1);

        var req = new AttackRequest(CharacterSkill.ComboAttack, perHit, lvl + 1, AttackFlags.Physical, AttackElement.None);
        req.AccuracyRatio = 120;
        var res = source.CalculateCombatResult(target, req);
        
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);
        
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.ComboAttack, lvl, res);
    }
}
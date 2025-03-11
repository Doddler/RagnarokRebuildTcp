using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.PiercingAttack, SkillClass.Physical, SkillTarget.Enemy)]
public class PiercingAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        var req = new AttackRequest(CharacterSkill.PiercingAttack, 1f + 0.1f * lvl, 1, AttackFlags.Physical | AttackFlags.IgnoreDefense, AttackElement.None);
        req.AccuracyRatio = 100 + lvl * 10;
        var res = source.CalculateCombatResult(target, req);

        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.PiercingAttack, lvl, res);
    }
}
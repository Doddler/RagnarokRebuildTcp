using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.DarkStrike)]
public class DarkStrikeHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (lvl < 0 || lvl > 10)
            lvl = 10;

        if (target == null || !target.IsValidTarget(source))
            return;

        var hits = 1 + (lvl - 1) / 2;

        var res = source.CalculateCombatResult(target, 1f, hits, AttackFlags.Magical, CharacterSkill.DarkStrike, AttackElement.Dark);
        res.AttackMotionTime = 0.75f;

        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.DarkStrike, lvl, res);
    }
}
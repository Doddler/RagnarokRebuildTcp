using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.DarkThunder, SkillClass.Magic)]
public class DarkThunderHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        //return 0f;

        if (lvl < 0 || lvl > 10)
            lvl = 10;

        return 2f + lvl * 0.5f;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var hitCount = lvl + 2;
        var knockBack = int.Clamp(hitCount, 3, 12);

        if (target == null || !target.IsValidTarget(source))
            return;

        var res = source.CalculateCombatResult(target, 1, hitCount, AttackFlags.Magical, CharacterSkill.DarkThunder, AttackElement.Dark);
        res.KnockBack = (byte)knockBack;
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.DarkThunder, lvl, res);



    }
}
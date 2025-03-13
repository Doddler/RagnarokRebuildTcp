using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.SoulStrike, SkillClass.Magic)]
public class SoulStrikeHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (lvl < 0 || lvl > 10)
            lvl = 10;

        if (target == null || !target.IsValidTarget(source))
            return;

        var hits = 1 + (lvl - 1) / 2;

        var res = source.CalculateCombatResult(target, 1.2f, hits, AttackFlags.Magical, CharacterSkill.SoulStrike, AttackElement.Ghost);
        if (target.IsElementBaseType(CharacterElement.Undead1) && res.Damage > 0)
            res.Damage = res.Damage * (100 + 5 * lvl) / 100; //5% bonus against undead per level
        res.Time = 0.75f;

        source.ApplyAfterCastDelay(1.2f - ((lvl + 1) % 2) * 0.2f, ref res);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SoulStrike, lvl, res);
    }
}
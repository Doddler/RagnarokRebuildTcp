using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Numerics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant;

[SkillHandler(CharacterSkill.Mammonite, SkillClass.Physical)]
public class MammoniteHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect)
    {
        if (source.Character.Type == CharacterType.Player &&
            GetMammoniteCost(source.Player, lvl) > source.Player.GetZeny())
            return SkillValidationResult.InsufficientZeny;

        return StandardValidation(source, target, position);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        if (source.Character.Type == CharacterType.Player)
        {
            var cost = GetMammoniteCost(source.Player, lvl);
            source.Player.DropZeny(cost);
            CommandBuilder.SendUpdateZeny(source.Player);
        }

        var res = source.CalculateCombatResult(target, 1f + lvl * 0.5f, 1, AttackFlags.Physical, CharacterSkill.Mammonite);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Mammonite, lvl, res);
    }

    private int GetMammoniteCost(Player player, int level)
    {
        var cost = 50 * level;
        var discountLvl = player.MaxLearnedLevelOfSkill(CharacterSkill.Discount);
        var discount = discountLvl > 0 ? 5 + discountLvl * 2 : 0;
        cost -= cost * discount / 100;
        return cost;
    }
}
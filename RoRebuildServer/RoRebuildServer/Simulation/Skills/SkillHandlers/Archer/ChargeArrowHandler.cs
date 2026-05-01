using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer;

[SkillHandler(CharacterSkill.ChargeArrow, SkillClass.Physical, SkillTarget.Enemy)]
public class ChargeArrowHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource) =>
        ValidateTargetForAmmunitionWeapon(source, target, position, 12, AmmoType.Arrow);

    public override int GetSkillRange(CombatEntity source, int lvl)
    {
        if (source.Character.Type == CharacterType.Player)
            return 10 + source.Player.MaxLearnedLevelOfSkill(CharacterSkill.VultureEye) / 2;

        return 10;
    }

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.8f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        var res = source.CalculateCombatResult(target, 1.5f, 1, AttackFlags.Physical | AttackFlags.Ranged, CharacterSkill.ChargeArrow);
        res.KnockBack = 6;
        res.Time += source.Character.Position.DistanceTo(target.Character.Position) / ServerConfig.ArrowTravelTime;
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.ChargeArrow, lvl, res);
    }
}
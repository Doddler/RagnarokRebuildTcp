using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.SpearBoomerang, SkillClass.Physical)]
public class SpearBoomerangHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl)
    {
        return lvl switch
        {
            1 => 5,
            2 => 6,
            3 => 8,
            4 => 9,
            5 => 11,
            _ => 5,
        };
    }

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player && (source.Player.WeaponClass < 4 || source.Player.WeaponClass > 5))
            return SkillValidationResult.IncorrectWeapon; //spear only

        return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        var res = source.CalculateCombatResult(target, 1f + lvl * 0.5f, 1, AttackFlags.Physical | AttackFlags.Ranged, CharacterSkill.SpearBoomerang);
        res.AttackMotionTime = 0.2f; //throw motion is half as long as the attack one
        res.Time = Time.ElapsedTimeFloat + 0.2f + source.Character.Position.DistanceTo(target.Character.Position) / ServerConfig.ArrowTravelTime;
        source.ApplyCooldownForSupportSkillAction();
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SpearBoomerang, lvl, res);
    }
}
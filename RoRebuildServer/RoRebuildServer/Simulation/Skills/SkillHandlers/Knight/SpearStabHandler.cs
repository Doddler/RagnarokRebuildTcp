using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Linq;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.SpearStab, SkillClass.Physical)]
public class SpearStabHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 4;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        if (!isIndirect && source.Character.Type == CharacterType.Player && (source.Player.WeaponClass < 4 || source.Player.WeaponClass > 5))
            return SkillValidationResult.IncorrectWeapon; //spear only

        return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        lvl = lvl.Clamp(1, 10);
        var map = source.Character.Map;

        if (target == null || !target.IsValidTarget(source) || map == null)
            return;

        var req = new AttackRequest(CharacterSkill.SpearStab, 1f + lvl * 0.2f, 1, AttackFlags.Physical, AttackElement.None);
        var res = source.CalculateCombatResult(target, req);
        res.KnockBack = 6;
        if (target.Character.Position == source.Character.Position)
            res.AttackPosition = source.Character.Position - Directions.GetVectorForDirection(source.Character.FacingDirection); //if we're stacked knock back in view direction
        else
            source.Character.FaceTargetWithoutClientUpdate(target.Character); //if one of our targets later is stacked this will make us knock them in the right direction
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var srcPoint = source.Character.Position;
        var endPoint = target.Character.Position;
        var dist = (int)srcPoint.FloatDistance(endPoint);

        using var potentialTargets = EntityListPool.Get();
        var area = Area.CreateAroundTwoPoints(srcPoint, endPoint, 7 - dist);
        map.GatherEnemiesInArea(source.Character, area, potentialTargets, true, true);
        map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character);

        foreach (var potentialTarget in potentialTargets)
        {
            if (potentialTarget == target.Entity || !potentialTarget.TryGet<CombatEntity>(out var splashTarget))
                continue;

            if (!MathHelper.IsPointInLinePath(srcPoint, endPoint, splashTarget.Character.Position, 6, 1.1f))
                continue;

            var req2 = new AttackRequest(CharacterSkill.SpearStab, 1f + lvl * 0.2f, 1, AttackFlags.Physical, AttackElement.None);
            var res2 = source.CalculateCombatResult(splashTarget, req2);
            res2.KnockBack = 6;
            res2.IsIndirect = true;
            if (splashTarget.Character.Position == source.Character.Position)
                res2.AttackPosition = source.Character.Position - Directions.GetVectorForDirection(source.Character.FacingDirection); //if we're stacked knock back in view direction
            source.ExecuteCombatResult(res2, false);

            CommandBuilder.AttackMulti(source.Character, splashTarget.Character, res2, false);
        }

        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.SpearStab, lvl, res);
        CommandBuilder.ClearRecipients();
    }
}
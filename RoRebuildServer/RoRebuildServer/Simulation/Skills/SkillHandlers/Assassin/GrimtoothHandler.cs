using Microsoft.EntityFrameworkCore.Migrations;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Assassin;

[SkillHandler(CharacterSkill.Grimtooth, SkillClass.Physical, SkillTarget.Enemy)]
public class GrimtoothHandler : SkillHandlerBase
{
    public override bool UsableWhileHidden => true;
    public override int GetSkillRange(CombatEntity source, int lvl) => 1 + lvl;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player && !source.HasBodyState(BodyStateFlags.Hidden))
            return SkillValidationResult.MustBeUsedWhileHidden;

        return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        var map = source.Character.Map;
        if (map == null || target == null)
            return;

        map.AddVisiblePlayersAsPacketRecipients(source.Character);

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, 1, targetList, true, true);

        var attack = new AttackRequest(CharacterSkill.Grimtooth, 1 + 0.2f * lvl, 1, AttackFlags.Physical | AttackFlags.Ranged, AttackElement.None);


        foreach (var e in targetList)
        {
            if (e.TryGet<WorldObject>(out var blastTarget))
            {
                var res = source.CalculateCombatResult(blastTarget.CombatEntity, attack);

                var dist = source.Character.WorldPosition.DistanceTo(target.Character.WorldPosition);
                res.Time += dist * 0.02f;
                res.AttackPosition = position;

                source.ExecuteCombatResult(res, false);
                CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
            }
        }

        var emptyRes = DamageInfo.EmptyResult(source.Entity, target.Entity);
        if (!isIndirect)
        {
            emptyRes.IsIndirect = true;
            source.ApplyCooldownForAttackAction(position);
        }

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Grimtooth, lvl, emptyRes, isIndirect);
    }
}
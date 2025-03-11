using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using RoRebuildServer.Data;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer;

[SkillHandler(CharacterSkill.ArrowShower, SkillClass.Ranged, SkillTarget.Ground)]
public class ArrowShowerHandler : SkillHandlerBase
{
    public override int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => 2; //range 2 = 5x5

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (lvl < 0 || lvl > 10)
            lvl = 10;

        return 1.5f - lvl * 0.1f;
    }

    //new arrow shower is 5x5
    //it deals up to 250% on the target cell, then falls off to 200% at 1 tile away and 150% at 2 tiles
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        lvl = int.Clamp(lvl, 1, 10);

        map.AddVisiblePlayersAsPacketRecipients(source.Character);

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, position, 2, targetList, true, true);

        var maxRatio = 1.5f + lvl * 0.1f;
        var midRatio = 1f + lvl * 0.1f;
        var minRatio = 0.8f + lvl * 0.1f;

        var attack = new AttackRequest(CharacterSkill.ArrowShower, 1, 1, AttackFlags.Physical | AttackFlags.Ranged, AttackElement.Neutral);

        foreach (var e in targetList)
        {
            if (e.TryGet<WorldObject>(out var blastTarget))
            {
                var dist = position.SquareDistance(blastTarget.Position);
                attack.AttackMultiplier = dist switch
                {
                    0 => maxRatio,
                    1 => midRatio,
                    _ => minRatio
                };

                var res = source.CalculateCombatResult(blastTarget.CombatEntity, attack);
                res.KnockBack = 2;
                res.Time += source.Character.Position.DistanceTo(blastTarget.Position) / ServerConfig.ArrowTravelTime; //for animation
                res.AttackPosition = position;

                source.ExecuteCombatResult(res, false);
                CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
            }
        }
        
        source.ApplyCooldownForAttackAction(position);
        CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(source.Character, position, CharacterSkill.ArrowShower, lvl);
    }
}
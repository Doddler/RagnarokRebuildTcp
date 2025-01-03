using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.FireBall, SkillClass.Magic)]
public class FireBallHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1.5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || !target.IsTargetable)
            return;

        lvl = int.Clamp(lvl, 1, 10);
        var map = source.Character.Map!;
        
        map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character); //combines src and target visibility lists

        var skillModifier = 1.4f + 0.2f * lvl;
        var attack = new AttackRequest(CharacterSkill.FireBall, skillModifier, 1, AttackFlags.Magical, AttackElement.Fire);

        //initial hit against the targeted enemy
        var res = source.CalculateCombatResult(target, attack);
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.FireBall, lvl, res);
        source.ExecuteCombatResult(res, false);

        //splash damage to targets nearby
        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, 2, targetList, true, true);

        foreach (var e in targetList)
        {
            if (e == target.Entity)
                continue;

            if (!e.TryGet<WorldObject>(out var blastTarget))
                continue;

            attack.AttackMultiplier = source.Character.Position.SquareDistance(blastTarget.Position) <= 1 ? skillModifier : skillModifier * 0.7f;
            res = source.CalculateCombatResult(e.Get<CombatEntity>(), attack);

            source.ExecuteCombatResult(res, false);
            CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
        }

        source.ApplyCooldownForAttackAction(position);
        CommandBuilder.ClearRecipients();
    }
}
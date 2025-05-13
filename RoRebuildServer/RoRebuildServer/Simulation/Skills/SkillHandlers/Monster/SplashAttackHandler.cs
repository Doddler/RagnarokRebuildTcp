using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.SplashAttack, SkillClass.Physical, SkillTarget.Enemy)]
public class SplashAttackHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        var map = source.Character.Map;
        Debug.Assert(map != null);

        map.AddVisiblePlayersAsPacketRecipients(source.Character);

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, 3, targetList, true, true);

        var attack = new AttackRequest(CharacterSkill.SplashAttack, 1, 1, AttackFlags.Physical, AttackElement.None);

        foreach (var e in targetList)
        {
            if (!e.TryGet<WorldObject>(out var blastTarget))
                continue;

            var res = source.CalculateCombatResult(e.Get<CombatEntity>(), attack);
            res.IsIndirect = true;

            source.ExecuteCombatResult(res, false);

            CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
        }

        source.ApplyCooldownForAttackAction(position);

        var skillResult = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.SplashAttack);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SplashAttack, lvl, skillResult);
    }
}
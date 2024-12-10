using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using System.Diagnostics;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.MagnumBreak, SkillClass.Physical, SkillTarget.Self)]
public class MagnumBreakHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        lvl = int.Clamp(lvl, 1, 10);

        map.AddVisiblePlayersAsPacketRecipients(source.Character);

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, source.Character.Position, 2, targetList, true, true);

        var attack = new AttackRequest(CharacterSkill.MagnumBreak, 1 + 0.2f * lvl, 1, AttackFlags.Physical, AttackElement.Fire);
        attack.AccuracyRatio = 100 + lvl * 10;

        foreach (var e in targetList)
        {
            var res = source.CalculateCombatResult(e.Get<CombatEntity>(), attack);
            res.KnockBack = 2;
            res.AttackPosition = source.Character.Position;

            source.ExecuteCombatResult(res, false);
            
            if (e.TryGet<WorldObject>(out var blastTarget))
                CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
        }

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.MagnumBreak, 15);
        source.AddStatusEffect(status);

        source.ApplyCooldownForAttackAction(position);
        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.MagnumBreak, lvl);
    }
}
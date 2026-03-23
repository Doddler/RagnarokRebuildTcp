using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[SkillHandler(CharacterSkill.Detect, SkillClass.Physical, SkillTarget.Ground)]
public class DetectHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, source.Character.Position, 5, targetList, false, false);
        foreach (var e in targetList)
        {
            if (e.TryGet<CombatEntity>(out var nearbyEnemy))
                nearbyEnemy.RemoveStatusOfGroupIfExists("Hiding");
        }

        CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(source.Character, position, CharacterSkill.Detect, lvl);
    }
}

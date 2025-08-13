using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Monster;
//using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.RecallMinion, SkillClass.Magic, SkillTarget.Self)]
public class RecallMinionHandler : SkillHandlerBase
{
    private bool NeedsRecall(EntityComponents.Monster m)
    {
        Debug.Assert(m.Children != null); //should be impossible
        for (var i = 0; i < m.ChildCount; i++)
        {
            var minion = m.Children[i];
            if (!minion.TryGet<WorldObject>(out var ch))
                continue;

            if ((m.CurrentAiState != MonsterAiState.StateIdle && ch.Monster.CurrentAiState == MonsterAiState.StateIdle) ||
                ch.Position.SquareDistance(m.Character.Position) > 9)
                return true;
        }

        return false;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (source.Character.Type != CharacterType.Monster)
            return;

        var m = source.Character.Monster;
        var map = source.Character.Map;
        if (m.ChildCount == 0 || m.Children == null || map == null) return;

        m.Children.ClearInactive();

        //if (!NeedsRecall(m)) //if our minions are all nearby and they're either occupied or idle with the boss, we can skip
        //    return;

        var area = Area.CreateAroundPoint(source.Character.Position, 2);
        for(var i = 0; i < m.ChildCount; i++)
        {
            var minion = m.Children[i];
            if (!minion.TryGet<WorldObject>(out var ch))
                continue;

            map.TeleportEntity(ref minion, ch, map.GetRandomWalkablePositionInArea(area), CharacterRemovalReason.Teleport, false);
        }
    }
}
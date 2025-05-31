using Microsoft.CodeAnalysis.CSharp.Syntax;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using System.Linq;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.MonsterSnap, SkillClass.None, SkillTarget.Enemy)]
public class MonsterSnapHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 14;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;

        if (target == null || map == null || !target.IsValidTarget(source))
            return;
        
        if (!map.WalkData.HasLineOfSight(source.Character.Position, target.Character.Position))
            return;

        var angle = target.Character.Position.Angle(source.Character.Position);
        var dir = Directions.GetFacingForAngle(angle);
        
        var targetPos = target.Character.Position.AddDirectionToPosition(dir);
        if (!map.WalkData.IsCellWalkable(targetPos))
            targetPos = target.Character.Position;

        map.ChangeEntityPosition3(source.Character, source.Character.WorldPosition, targetPos, false);
        source.Character.StopMovingImmediately();

    }
}
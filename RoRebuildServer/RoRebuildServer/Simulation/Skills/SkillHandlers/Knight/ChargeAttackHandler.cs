using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.ChargeAttack, SkillClass.Physical, SkillTarget.Enemy)]
public class ChargeAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 14;

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (target == null)
            return 0;

        var distance = source.Character.Position.DistanceTo(target.Character.Position);
        return float.Clamp(distance / 28f, 0.1f, 0.5f);
    }

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return SkillValidationResult.InvalidTarget;

        if (!source.Character.Map?.WalkData.HasDirectPathAccess(source.Character.Position, target.Character.Position) ?? true)
            return SkillValidationResult.NoLineOfSight;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return false;

        return source.Character.Map?.WalkData.HasDirectPathAccess(source.Character.Position, target.Character.Position) ?? false;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        lvl = lvl.Clamp(1, 10);
        var map = source.Character.Map;

        if (target == null || !target.IsValidTarget(source) || map == null)
            return;

        var distance = source.Character.Position.DistanceTo(target.Character.Position);
        var ratio = float.Clamp(1f + distance / 3f, 1f, 5f);

        var req = new AttackRequest(CharacterSkill.ChargeAttack, ratio, 1, AttackFlags.Physical, AttackElement.None);
        req.AccuracyRatio = 100 + lvl * 5;
        var res = source.CalculateCombatResult(target, req);

        if (res.IsDamageResult)
            res.KnockBack = (byte)int.Clamp(1 + distance / 5, 1, 3);

        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        if (source.Character.Type == CharacterType.Player)
            source.Player.SkillSpecificCooldowns[CharacterSkill.ChargeAttack] = Time.ElapsedTime + 5f;

        //gap closer
        var angle = target.Character.Position.Angle(source.Character.Position);
        var dir = Directions.GetFacingForAngle(angle);

        var targetPos = target.Character.Position.AddDirectionToPosition(dir);
        if (!map.WalkData.IsCellWalkable(targetPos))
            targetPos = target.Character.Position;

        map.ChangeEntityPosition3(source.Character, source.Character.WorldPosition, targetPos, false);
        source.Character.StopMovingImmediately();

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.ChargeAttack, lvl, res);
    }
}
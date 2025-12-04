using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.CounterAttack, SkillClass.Physical, SkillTarget.Self)]
public class CounterAttackHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.4f * lvl;

    public override bool ShouldSkillCostSp(CombatEntity source) => false;

    public override void OnHitEvent(CombatEntity owner, CombatEntity? attacker, SkillCastInfo info, ref AttackRequest req, ref DamageInfo di)
    {
        if (attacker == null || di.AttackSkill != CharacterSkill.None)
            return;

        if (owner.Character.Position.SquareDistance(attacker.Character.Position) > owner.GetStat(CharacterStat.Range))
            return;

        if ((req.Flags & AttackFlags.Physical) == 0)
            return;

        var diff = MathHelper.AngleFromDirection(owner.Character.FacingDirection, attacker.Character.Position - owner.Character.Position);
        if (diff < -135 || diff > 135)
            return;

        if (owner.Character.Type == CharacterType.Player)
            owner.Player.TakeSpValue(2);

        owner.CancelCast(true);
        owner.Character.LookAtEntity(ref attacker.Entity);

        var counterReq = new AttackRequest(CharacterSkill.CounterAttack, 1, 1, AttackFlags.Physical | AttackFlags.GuaranteeCrit, AttackElement.None);
        var res = owner.CalculateCombatResult(attacker, counterReq);

        owner.ApplyCooldownForAttackAction(attacker);
        owner.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(owner.Character, attacker.Character, CharacterSkill.CounterAttack, info.Level, res);

        if (di.AttackSkill == CharacterSkill.None)
        {
            di.Damage = 0;
            di.Result = AttackResult.InvisibleMiss;
        }
    }

    public override SkillValidationResult StandardValidation(CombatEntity source, CombatEntity? target, Position position)
    {
        if (source.Character.Type == CharacterType.Player && source.GetStat(CharacterStat.Sp) < 2)
            return SkillValidationResult.InsufficientSp;

        return base.StandardValidation(source, target, position);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player)
            source.Player.TakeSpValue(2);

        CommandBuilder.ResetMotionAutoVis(source.Character);
    }
}
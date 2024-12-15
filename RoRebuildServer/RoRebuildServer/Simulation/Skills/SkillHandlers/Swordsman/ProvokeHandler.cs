using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using System;
using RebuildSharedData.ClientTypes;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.Provoke, SkillClass.Unique, SkillTarget.Enemy)]
public class ProvokeHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position)
    {
        if (target.IsElementBaseType(CharacterElement.Undead1))
            return SkillValidationResult.Failure;

        return StandardValidation(source, target, position);
    }

    public override int GetSkillRange(CombatEntity source, int lvl) => 9;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        target.Character.LastAttacked = source.Entity;
        source.ApplyCooldownForSupportSkillAction();

        if (target.Character.Type == CharacterType.Monster)
        {
            if (target.CanAttackTarget(source.Character))
                target.Character.Monster.Target = source.Entity;
        }

        var ch = source.Character;
        var di = DamageInfo.EmptyResult(source.Entity, target.Entity);
        di.AttackSkill = CharacterSkill.Provoke;
        //var applyStatus = true;

        if (target.Character.Type == CharacterType.Monster)
        {
            var mon = target.Character.Monster;
            mon.NotifyOfAttack(ref di);
        }

        if (target.Character.Type == CharacterType.Player)
        {
            if(target.IsCasting && target.CastInterruptionMode <= CastInterruptionMode.InterruptOnSkill)
                target.CancelCast();
        }

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Provoke, 30f, lvl, source.Character.Id);
        target.AddStatusEffect(status);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Provoke, lvl, di);
    }
}
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using System;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.Provoke, SkillClass.Unique, SkillTarget.Enemy)]
public class ProvokeHandler : SkillHandlerBase
{
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
        var applyStatus = true;

        if (target.Character.Type == CharacterType.Monster)
        {
            var mon = target.Character.Monster;
            mon.NotifyOfAttack(ref di);
        }

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Provoke, 30f, lvl, source.Character.Id);
        target.AddStatusEffect(status);

        ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Provoke, lvl, di);
        CommandBuilder.ClearRecipients();
    }
}
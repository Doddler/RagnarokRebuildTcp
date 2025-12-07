using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.PoisonReact, StatusClientVisibility.Owner)]
public class StatusPoisonReact : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnCalculateDamageTaken;

    public override StatusUpdateResult OnCalculateDamage(CombatEntity ch, ref StatusEffectState state, ref AttackRequest req, ref DamageInfo info)
    {

        if (!info.Source.TryGet<CombatEntity>(out var attacker))
            return StatusUpdateResult.Continue;

        if ((req.Flags & AttackFlags.Physical) == 0)
            return StatusUpdateResult.Continue;

        var isPoison = req.Element == AttackElement.Poison;
        if (!isPoison && attacker.IsElementBaseType(CharacterElement.Poison1))
        {
            if(info.IsDamageResult || req.SkillSource != CharacterSkill.None)
                isPoison = true;
        }

        if (isPoison)
        {
            if (ch.Character.Position.DistanceTo(attacker.Character.Position) > 14)
                return StatusUpdateResult.Continue;

            if(ch.Character.State == CharacterState.Sitting)
                ch.Character.SitStand(false);

            var res = ch.CalculateMeleeAttack(attacker, (0.5f + 0.1f * state.Value1) * state.Value2);
            res.AttackSkill = CharacterSkill.PoisonReact;
            res.IsIndirect = true;

            ch.ExecuteCombatResult(res, false);

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(ch.Character, attacker.Character, CharacterSkill.PoisonReact, 1, res);

            if (info.AttackSkill == CharacterSkill.None)
            {
                info.Damage = 0;
                info.Result = AttackResult.InvisibleMiss;
            }

            if (res.IsDamageResult)
                ch.TryPoisonOnTarget(attacker, 100_000, true, res.TotalDamage / 2);

            return StatusUpdateResult.EndStatus;
        }

        if (!info.IsDamageResult || GameRandom.Next(0, 100) > 50)
            return StatusUpdateResult.Continue;

        if (ch.Character.Position.DistanceTo(attacker.Character.Position) >= 5)
            return StatusUpdateResult.Continue;

        var level = 1;
        if (ch.Character.Type == CharacterType.Player)
            level = ch.Player.MaxAvailableLevelOfSkill(CharacterSkill.Envenom);
        
        var skill = new SkillCastInfo()
        {
            Skill = CharacterSkill.Envenom,
            Level = level,
            TargetEntity = attacker.Entity,
            IsIndirect = true
        };
        if (SkillHandler.ValidateTarget(skill, ch, true) == SkillValidationResult.Success)
        {
            SkillHandler.ExecuteSkill(skill, ch);
            state.Value2 -= 1;

            if (state.Value2 == 0)
                return StatusUpdateResult.EndStatus;
        }

        return StatusUpdateResult.Continue;
    }
}

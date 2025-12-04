using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.Falcon, StatusClientVisibility.Owner, StatusEffectFlags.NoSave | StatusEffectFlags.StayOnClear)]
public class StatusFalcon : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

    public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (ch.Character.Type != CharacterType.Player || !info.IsDamageResult || !info.Target.TryGet<CombatEntity>(out var target))
            return StatusUpdateResult.Continue;

        var maxSkill = ch.Player.MaxLearnedLevelOfSkill(CharacterSkill.BlitzBeat);
        if (maxSkill == 0)
            return StatusUpdateResult.Continue;

        var chance = 10 + ch.GetEffectiveStat(CharacterStat.Luck) * 3;

        //custom change: gear that adds crit boosts your base chance of triggering auto blitz
        chance += chance * (ch.GetEffectiveStat(CharacterStat.AddCrit) + ch.GetBonusCritRateVsTarget(target)) / 100;

        if (GameRandom.NextInclusive(1000) < chance)
        {
            var cast = new SkillCastInfo()
            {
                Skill = CharacterSkill.BlitzBeat,
                Level = maxSkill,
                TargetEntity = info.Target,
                TargetedPosition = Position.Invalid,
                IsIndirect = true
            };

            if (SkillHandler.ValidateTarget(cast, ch, true) == SkillValidationResult.Success)
                SkillHandler.ExecuteSkill(cast, ch);
        }


        return StatusUpdateResult.Continue;
    }
}
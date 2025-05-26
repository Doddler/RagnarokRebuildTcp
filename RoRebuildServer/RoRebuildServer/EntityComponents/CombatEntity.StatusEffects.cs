using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.ScriptSystem;
using RoRebuildServer.Simulation.Skills;

namespace RoRebuildServer.EntityComponents;

public partial class CombatEntity
{
    private void TriggerOnAttackEffects(CombatEntity target, AttackRequest req, ref DamageInfo res)
    {
        if (!req.Flags.HasFlag(AttackFlags.Physical))
            return;

        if (Character.Type == CharacterType.Player && !req.Flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects))
        {
            var chance = GetStat(CharacterStat.OnAttackStun);
            if (chance > 0)
                TryStunTarget(target, chance);

            chance = GetStat(CharacterStat.OnAttackPoison);
            if (chance > 0)
                TryPoisonOnTarget(target, chance);

            chance = GetStat(CharacterStat.OnAttackBlind);
            if (chance > 0)
                TryBlindTarget(target, chance, res.AttackMotionTime + 0.5f); //delayed a little so you can actually hear the blind sound

            chance = GetStat(CharacterStat.OnAttackFreeze);
            if (chance > 0)
                TryFreezeTarget(target, chance, res.AttackMotionTime + 0.1f); //don't want our damage application to cancel the status

            chance = GetStat(CharacterStat.OnAttackSleep);
            if (chance > 0)
                TrySleepTarget(target, chance, res.AttackMotionTime + 0.1f); //don't want our damage application to cancel the status

            chance = GetStat(CharacterStat.OnAttackSilence);
            if (chance > 0)
                TrySilenceTarget(target, chance);

            chance = GetStat(CharacterStat.OnAttackStone);
            if (chance > 0)
                TryPetrifyTarget(target, chance, 1f);

            chance = GetStat(CharacterStat.OnAttackCurse);
            if (chance > 0)
                TryCurseTarget(target, chance);

            chance = GetStat(CharacterStat.SpDrainChance);
            if(chance > 0 && GameRandom.Next(0, 100) < chance)
                RecoverSp(res.Damage * res.HitCount * GetStat(CharacterStat.SpDrainAmount) / 100);

            chance = GetStat(CharacterStat.HpDrainChance);
            if (chance > 0 && GameRandom.Next(0, 100) < chance)
                HealHp(res.Damage * res.HitCount * GetStat(CharacterStat.HpDrainAmount) / 100);

            var val = GetStat(CharacterStat.SpOnAttack);
            if(val > 0)
                RecoverSp(val);

        }
    }

    private void TriggerWhenAttackedEffects(CombatEntity attacker, AttackRequest req, ref DamageInfo res)
    {
        if (!req.Flags.HasFlag(AttackFlags.Physical))
            return;

        if (Character.Type == CharacterType.Player && !req.Flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects))
        {
            var chance = GetStat(CharacterStat.WhenAttackedStun);
            if (chance > 0)
                TryStunTarget(attacker, chance);

            chance = GetStat(CharacterStat.WhenAttackedPoison);
            if (chance > 0)
                TryPoisonOnTarget(attacker, chance);

            chance = GetStat(CharacterStat.WhenAttackedBlind);
            if (chance > 0)
                TryBlindTarget(attacker, chance);

            chance = GetStat(CharacterStat.WhenAttackedFreeze);
            if (chance > 0)
                TryFreezeTarget(attacker, chance);

            chance = GetStat(CharacterStat.WhenAttackedSleep);
            if (chance > 0)
                TrySleepTarget(attacker, chance);

            chance = GetStat(CharacterStat.WhenAttackedSilence);
            if (chance > 0)
                TrySilenceTarget(attacker, chance);

            chance = GetStat(CharacterStat.WhenAttackedStone);
            if (chance > 0)
                TryPetrifyTarget(attacker, chance, 1f);

            chance = GetStat(CharacterStat.WhenAttackedCurse);
            if (chance > 0)
                TryCurseTarget(attacker, chance);
        }
    }

    private void TriggerAutoSpell(CombatEntity target, AttackRequest req, ref DamageInfo res)
    {
        if (Character.Type != CharacterType.Player || req.SkillSource != CharacterSkill.None ||
            !req.Flags.HasFlag(AttackFlags.Physical) || !res.IsDamageResult)
            return;

        foreach (var (_, effect) in Player.Equipment.AutoSpellSkillsOnAttack)
        {
            if (GameRandom.Next(0, 1000) < effect.Chance)
            {
                var skillCast = new SkillCastInfo()
                {
                    Skill = effect.Skill,
                    Level = effect.Level,
                    TargetEntity = target.Entity,
                    IsIndirect = true
                };

                if (SkillHandler.ValidateTarget(skillCast, this) == SkillValidationResult.Success)
                {
                    SkillHandler.ExecuteSkill(skillCast, this);
                }
            }
        }
    }

    public bool TryStunTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasBodyState(BodyStateFlags.DisablingState))
            return false;

        var vit = target.GetEffectiveStat(CharacterStat.Vit);
        var luk = target.GetEffectiveStat(CharacterStat.Luk);

        if (target.Character.Type == CharacterType.Player)
            vit = vit * 3 / 2; //1.5x for players
        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            vit = vit * 5 / 2; //2.5x

        var resist = MathHelper.PowScaleDown(vit);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistStunStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(vit + GameRandom.Next(0, luk));
        var len = 5f * timeResist;

        var durationResist = 100 - target.GetStat(CharacterStat.DecreaseStunDuration);
        if (durationResist != 100)
            len = len * durationResist / 100;

        if (len <= 0)
            return false;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Stun, len);
        target.AddStatusEffect(status, false, delayApply);
        return true;
    }

    public bool TryPoisonOnTarget(CombatEntity target, int chanceIn1000, bool scaleDuration = true, int baseDamage = 0, float baseDuration = 24f, float delayApply = 0.3f)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Poison) || target.IsElementBaseType(CharacterElement.Undead1))
            return false;

        if (target.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Detoxify))
            return false;

        var vit = target.GetEffectiveStat(CharacterStat.Vit);

        if (target.Character.Type == CharacterType.Player)
            vit = vit * 3 / 2; //1.5x for players

        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            vit = vit * 5 / 2; //2.5x for boss

        var resist = MathHelper.PowScaleDown(vit);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistPoisonStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        if (baseDamage == 0)
        {
            var req = new AttackRequest(0.5f, 1, AttackFlags.PhysicalStatusTest,
                AttackElement.Neutral); //element doesn't matter

            var poisonDamage = CalculateCombatResult(target, req);
            baseDamage = poisonDamage.Damage;
        }

        var damageResist = 100 - target.GetStat(CharacterStat.DecreasePoisonStatusDamage);
        if (damageResist != 100)
            baseDamage = baseDamage * damageResist / 100;

        var len = baseDuration;
        if (scaleDuration)
            len *= MathHelper.PowScaleDown(vit / 2);

        len = 15;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Poison, len + 1.5f, Character.Id, baseDamage);
        target.AddStatusEffect(status, true, delayApply);

        return true;
    }

    public bool TryFreezeTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasBodyState(BodyStateFlags.DisablingState))
            return false;

        var mdef = target.GetEffectiveStat(CharacterStat.MDef);
        var luk = target.GetEffectiveStat(CharacterStat.Luk);

        if (target.GetSpecialType() == CharacterSpecialType.Boss || target.IsElementBaseType(CharacterElement.Undead1))
            return false;

        var resist = MathHelper.PowScaleDown(mdef);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistFreezeStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(mdef + GameRandom.Next(0, luk));
        var len = 12f * timeResist;

        var durationResist = 100 - target.GetStat(CharacterStat.DecreaseFreezeDuration);
        if (durationResist != 100)
            len = len * durationResist / 100;

        if (len <= 0)
            return false;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Frozen, len);
        target.AddStatusEffect(status, false, delayApply);
        return true;
    }

    public bool TryBlindTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Blind))
            return false;

        var luk = target.GetEffectiveStat(CharacterStat.Luk);
        var mnd = target.GetEffectiveStat(CharacterStat.Int);
        var vit = target.GetEffectiveStat(CharacterStat.Vit);

        var rVal = (mnd + vit) / 2;
        var resist = MathHelper.PowScaleDown(rVal);

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(rVal + GameRandom.Next(0, luk));
        var len = 30f * timeResist;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Blind, len);
        target.AddStatusEffect(status, false, delayApply);
        return true;
    }

    public bool TrySleepTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasBodyState(BodyStateFlags.DisablingState) || target.GetSpecialType() == CharacterSpecialType.Boss)
            return false;

        var luk = target.GetEffectiveStat(CharacterStat.Luk);
        var mnd = target.GetEffectiveStat(CharacterStat.Int);

        var rVal = mnd;
        var resist = MathHelper.PowScaleDown(rVal);

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(rVal + GameRandom.Next(0, luk));
        var len = 30f * timeResist;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Sleep, len);
        target.AddStatusEffect(status, false, delayApply);
        return true;
    }

    public bool TrySilenceTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Silence) || target.GetSpecialType() == CharacterSpecialType.Boss)
            return false;

        var luk = target.GetEffectiveStat(CharacterStat.Luk);
        var vit = target.GetEffectiveStat(CharacterStat.Vit);

        var resist = MathHelper.PowScaleDown(luk);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistSilenceStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(vit + GameRandom.Next(0, luk));
        if (resistChance != 100)
            timeResist = resist * resistChance / 100;
        var len = 30f * timeResist;

        target.CancelCast();

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Silence, len);
        target.AddStatusEffect(status, false, delayApply);
        return true;
    }

    public bool TryCurseTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Curse) || target.GetSpecialType() == CharacterSpecialType.Boss)
            return false;

        var luk = target.GetEffectiveStat(CharacterStat.Luk);
        var vit = target.GetEffectiveStat(CharacterStat.Vit);

        var resist = MathHelper.PowScaleDown(luk);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistCurseStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(vit + GameRandom.Next(0, luk));
        if (resistChance != 100)
            timeResist = resist * resistChance / 100;
        var len = 30f * timeResist;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Curse, len);
        target.AddStatusEffect(status, false, delayApply);
        return true;
    }

    public bool TryPetrifyTarget(CombatEntity target, int chanceIn1000, float petrifyTime, float delayApply = 0f)
    {
        if (target.HasBodyState(BodyStateFlags.DisablingState) || target.HasStatusEffectOfType(CharacterStatusEffect.Petrifying) || target.GetSpecialType() == CharacterSpecialType.Boss)
            return false;

        var mdef = target.GetEffectiveStat(CharacterStat.MDef);
        var luk = target.GetEffectiveStat(CharacterStat.Luk);

        var resist = MathHelper.PowScaleDown(mdef);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistStoneStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(mdef + GameRandom.Next(0, luk));
        var len = 20f * timeResist;

        var durationResist = 100 - target.GetStat(CharacterStat.ResistStoneStatus);
        if (durationResist != 100)
            len = len * durationResist / 100;

        if (len <= 0)
            return false;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Petrifying, delayApply + petrifyTime + 0.1f);
        target.AddStatusEffect(status, false, delayApply);

        var status2 = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Stone, delayApply + len + petrifyTime);
        target.AddStatusEffect(status2, true, petrifyTime + delayApply);
        return true;
    }

    [ScriptUseable]
    public bool CleanseStatusEffect(StatusCleanseTarget target)
    {
        if (statusContainer == null)
            return false; //we have no status effects

        var hasUpdate = false;

        if ((target & StatusCleanseTarget.Poison) > 0)
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Poison);

        if ((target & StatusCleanseTarget.Silence) > 0 && HasBodyState(BodyStateFlags.Silence))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Silence);
        
        if ((target & StatusCleanseTarget.Blind) > 0 && HasBodyState(BodyStateFlags.Blind))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Blind);

        if ((target & StatusCleanseTarget.Confusion) > 0 && HasBodyState(BodyStateFlags.Confusion))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Confusion);

        if ((target & StatusCleanseTarget.Hallucination) > 0 && HasBodyState(BodyStateFlags.Hallucination))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Hallucination);

        if ((target & StatusCleanseTarget.Curse) > 0 && HasBodyState(BodyStateFlags.Curse))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Curse);

        if ((target & StatusCleanseTarget.Petrify) > 0 && HasBodyState(BodyStateFlags.Petrified))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Stone);

        if (hasUpdate)
            UpdateStats();

        return hasUpdate;
    }
}

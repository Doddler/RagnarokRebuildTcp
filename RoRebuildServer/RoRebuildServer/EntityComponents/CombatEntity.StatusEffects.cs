using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.ScriptSystem;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;

namespace RoRebuildServer.EntityComponents;

public partial class CombatEntity
{
    private void TryTriggerStatus(StatusTriggerFlags status, CombatEntity target, int chance, ref DamageInfo res, bool useDamage = true)
    {
        switch (status)
        {
            case StatusTriggerFlags.Blind:
                TryBlindTarget(target, chance, res.TimeInSeconds + 0.5f); //delayed a little so you can actually hear the blind sound
                break;
            case StatusTriggerFlags.Silence:
                TrySilenceTarget(target, chance, res.TimeInSeconds);
                break;
            case StatusTriggerFlags.Curse:
                TryCurseTarget(target, chance, res.TimeInSeconds);
                break;
            case StatusTriggerFlags.Poison:
                TryPoisonOnTarget(target, chance, true, useDamage ? (res.Damage * res.HitCount / 2) : 0, 24f, res.TimeInSeconds);
                break;
            case StatusTriggerFlags.Confusion:
                break;
            case StatusTriggerFlags.HeavyPoison:
                break;
            case StatusTriggerFlags.Bleeding:
                break;
            case StatusTriggerFlags.Stun:
                TryStunTarget(target, chance, res.TimeInSeconds);
                break;
            case StatusTriggerFlags.Stone:
                TryPetrifyTarget(target, chance, 1f, res.TimeInSeconds);
                break;
            case StatusTriggerFlags.Freeze:
                TryFreezeTarget(target, chance, res.TimeInSeconds + 0.1f); //don't want our damage application to cancel the status
                break;
            case StatusTriggerFlags.Sleep:
                TrySleepTarget(target, chance, res.TimeInSeconds + 0.1f); //don't want our damage application to cancel the status
                break;
        }
    }

    private void TriggerOnAttackEffects(CombatEntity target, AttackRequest req, ref DamageInfo res, bool isRanged)
    {
        if (req.Flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects) || Character.Type != CharacterType.Player)
            return;

        var isPhysical = req.Flags.HasFlag(AttackFlags.Physical);

        if (isPhysical)
        {
            if (!isRanged && Player.OnMeleeAttackStatusFlags > 0)
            {
                var flags = Player.OnMeleeAttackStatusFlags;
                var count = (int)(CharacterStat.OnMeleeAttackLast - CharacterStat.OnMeleeAttackFirst);
                for (var i = 0; i < count; i++)
                {
                    var idx = 1 << i;
                    if ((idx & (int)flags) <= 0)
                        continue;

                    var chance = GetStat(CharacterStat.OnMeleeAttackFirst + i);
                    TryTriggerStatus((StatusTriggerFlags)idx, target, chance, ref res);
                }
            }

            if (isRanged && Player.OnRangedAttackStatusFlags > 0)
            {
                var flags = Player.OnRangedAttackStatusFlags;
                var count = (int)(CharacterStat.OnRangedAttackLast - CharacterStat.OnRangedAttackFirst);
                for (var i = 0; i < count; i++)
                {
                    var idx = 1 << i;
                    if ((idx & (int)flags) <= 0)
                        continue;

                    var chance = GetStat(CharacterStat.OnRangedAttackFirst + i);
                    TryTriggerStatus((StatusTriggerFlags)idx, target, chance, ref res);
                }
            }
        }

        var attackTriggerFlags = Player.OnAttackTriggerFlags;
        if (attackTriggerFlags > 0)
        {
            var race = target.GetRace();

            if ((attackTriggerFlags & AttackEffectTriggers.SpDrain) > 0)
            {
                var chance = GetStat(CharacterStat.SpDrainChance);
                if (chance > 0 && GameRandom.Next(0, 100) < chance)
                    RecoverSp(res.Damage * res.HitCount * GetStat(CharacterStat.SpDrainAmount) / 100);

                var pureDrain = GetStat(CharacterStat.PureSpDrain);
                if (pureDrain > 0)
                    RecoverSp(res.Damage * res.HitCount * pureDrain / 1000);
            }

            if ((attackTriggerFlags & AttackEffectTriggers.HpDrain) > 0)
            {
                var chance = GetStat(CharacterStat.HpDrainChance);
                if (chance > 0 && GameRandom.Next(0, 100) < chance)
                    HealHp(res.Damage * res.HitCount * GetStat(CharacterStat.HpDrainAmount) / 100);

                var pureDrain = GetStat(CharacterStat.PureHpDrain);
                if (pureDrain > 0)
                    HealHp(res.Damage * res.HitCount * pureDrain / 1000);
            }

            if ((attackTriggerFlags & AttackEffectTriggers.HpOnAttack) > 0)
            {
                var val = GetStat(CharacterStat.HpGainOnAttack);
                if (val > 0)
                    HealHp(val, true);

                val = GetStat(CharacterStat.HpGainOnAttackRaceFormless + (int)race);
                if (val > 0)
                    HealHp(val, true);
            }

            if ((attackTriggerFlags & AttackEffectTriggers.SpOnAttack) > 0)
            {
                var val = GetStat(CharacterStat.SpGainOnAttack);
                if (val > 0)
                    RecoverSpFixed(val);

                val = GetStat(CharacterStat.SpGainOnAttackRaceFormless + (int)race);
                if (val > 0)
                    RecoverSpFixed(val);
            }

            if ((attackTriggerFlags & AttackEffectTriggers.KillOnAttack) > 0
                    && isPhysical && target.GetSpecialType() != CharacterSpecialType.Boss)
            {
                var val = GetStat(CharacterStat.KnockOutOnAttack);
                val += GetStat(CharacterStat.KnockOutOnAttackRaceFormless + (int)race);

                var lvlDiff = target.GetStat(CharacterStat.Level) - GetStat(CharacterStat.Level);
                if (lvlDiff > 0)
                    val -= lvlDiff;

                if (val > 0 && CheckLuckModifiedRandomChanceVsTarget(target, val, 1000))
                {
                    var curHp = target.GetStat(CharacterStat.Hp);
                    if (curHp > res.Damage)
                        res.Damage = curHp;
                }
            }
        }
    }

    public void TriggerOnKillEffects(CombatEntity target)
    {
        if ((Player.OnAttackTriggerFlags & AttackEffectTriggers.HpOnKill) > 0)
        {
            var val = GetStat(CharacterStat.HpGainOnKill);
            val += GetStat(CharacterStat.HpGainOnAttackRaceFormless + (int)target.GetRace());
            if (val > 0)
                HealHp(val, true);
        }

        if ((Player.OnAttackTriggerFlags & AttackEffectTriggers.SpOnKill) > 0)
        {
            var val = GetStat(CharacterStat.SpGainOnKill);
            val += GetStat(CharacterStat.SpGainOnAttackRaceFormless + (int)target.GetRace());
            if (val > 0)
                RecoverSp(val);
        }
    }

    private void TriggerWhenAttackedEffects(CombatEntity attacker, AttackRequest req, ref DamageInfo res)
    {
        if (!req.Flags.HasFlag(AttackFlags.Physical) || req.Flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects) || Character.Type != CharacterType.Player || !CanPerformIndirectActions())
            return;

        var flags = Player.WhenAttackedStatusFlags;
        var count = (int)(CharacterStat.WhenAttackedLast - CharacterStat.WhenAttackedFirst);
        for (var i = 0; i < count; i++)
        {
            var idx = 1 << i;
            if ((idx & (int)flags) <= 0)
                continue;

            var chance = GetStat(CharacterStat.WhenAttackedFirst + i);
            TryTriggerStatus((StatusTriggerFlags)idx, attacker, chance, ref res, false);
        }
    }

    private SkillCastInfo PrepareAutoSpellActivation(AutoSpellEffect effect, CombatEntity src, CombatEntity target)
    {
        var spellTarget = target.Entity;
        var pos = target.Character.Position;
        var targetType = effect.Target == SkillPreferredTarget.Any
            ? SkillHandler.GetPreferredSkillTarget(effect.Skill)
            : effect.Target;

        if (targetType == SkillPreferredTarget.Self)
        {
            spellTarget = Entity;
            pos = Character.Position;
        }

        return new SkillCastInfo()
        {
            Skill = effect.Skill,
            Level = effect.Level,
            TargetEntity = spellTarget,
            TargetedPosition = pos,
            IsIndirect = true
        };
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
                var skillCast = PrepareAutoSpellActivation(effect, this, target);

                if (SkillHandler.ValidateTarget(skillCast, this, true) == SkillValidationResult.Success)
                    SkillHandler.ExecuteSkill(skillCast, this);
            }
        }
    }

    private void TriggerWhenAttackedAutoSpell(CombatEntity attacker, AttackRequest req, ref DamageInfo res)
    {
        if (Character.Type != CharacterType.Player
            || !req.Flags.HasFlag(AttackFlags.Physical)
            || !res.IsDamageResult
            || !CanPerformIndirectActions()
            || Character.Position.DistanceTo(attacker.Character.Position) > 14)
            return;

        foreach (var (_, effect) in Player.Equipment.AutoSpellSkillsWhenAttacked)
        {
            if (GameRandom.Next(0, 1000) < effect.Chance)
            {
                var skillCast = PrepareAutoSpellActivation(effect, this, attacker);
                skillCast.CastTime = res.Time;

                if (SkillHandler.ValidateTarget(skillCast, this, true) == SkillValidationResult.Success)
                {
                    Player.IndirectCastQueue.Add(skillCast);
                    Player.IndirectCastQueue.Sort((a, b) => a.CastTime.CompareTo(b.CastTime));
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
            vit *= 2; //2x for boss

        var resist = MathHelper.PowScaleDown(vit);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistPoisonStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (chanceIn1000 < 100_000 && !CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
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

    public bool TrySilenceTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f, float baseLength = 30f)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Silence) || target.GetSpecialType() == CharacterSpecialType.Boss)
            return false;

        var luk = target.GetEffectiveStat(CharacterStat.Luk);
        var vit = target.GetEffectiveStat(CharacterStat.Vit);

        var resist = MathHelper.PowScaleDown(vit + luk);
        var resistChance = 100 - target.GetStat(CharacterStat.ResistSilenceStatus);
        if (resistChance != 100)
            resist = resist * resistChance / 100;

        if (!CheckLuckModifiedRandomChanceVsTarget(target, (int)(chanceIn1000 * resist), 1000))
            return false;

        var timeResist = MathHelper.PowScaleDown(vit + GameRandom.Next(0, luk));
        if (resistChance != 100)
            timeResist = resist * resistChance / 100;
        var len = baseLength * timeResist;

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

        if ((target & StatusCleanseTarget.Frozen) > 0 && HasBodyState(BodyStateFlags.Frozen))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Frozen);

        if ((target & StatusCleanseTarget.Stunned) > 0 && HasBodyState(BodyStateFlags.Stunned))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Stun);

        if ((target & StatusCleanseTarget.Sleep) > 0 && HasBodyState(BodyStateFlags.Sleep))
            hasUpdate |= statusContainer.RemoveStatusEffectOfType(CharacterStatusEffect.Sleep);

        if (hasUpdate)
            UpdateStats();

        return hasUpdate;
    }
}

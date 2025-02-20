using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

public partial class CombatEntity
{
    private void TriggerOnAttackEffects(CombatEntity target, AttackRequest req, ref DamageInfo res)
    {
        if (!req.Flags.HasFlag(AttackFlags.Physical))
            return;

        if (Character.Type == CharacterType.Player && !req.Flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects))
        {
            var stunChance = GetStat(CharacterStat.OnAttackStun);
            if (stunChance > 0)
                TryStunTarget(target, stunChance);
            var poisonChance = GetStat(CharacterStat.OnAttackPoison);
            if (poisonChance > 0)
                TryPoisonOnTarget(target, poisonChance);
            var blindChance = GetStat(CharacterStat.OnAttackBlind);
            if (blindChance > 0)
                TryBlindTarget(target, blindChance, res.AttackMotionTime + 0.5f); //delayed a little so you can actually hear the blind sound
            var freezeChance = GetStat(CharacterStat.OnAttackFreeze);
            if (freezeChance > 0)
                TryFreezeTarget(target, freezeChance, res.AttackMotionTime + 0.1f); //don't want our damage application to cancel the status
            var sleepChance = GetStat(CharacterStat.OnAttackSleep);
            if (sleepChance > 0)
                TrySleepTarget(target, sleepChance, res.AttackMotionTime + 0.1f); //don't want our damage application to cancel the status
        }
    }

    private void TriggerWhenAttackedEffects(CombatEntity attacker, AttackRequest req, ref DamageInfo res)
    {
        if (!req.Flags.HasFlag(AttackFlags.Physical))
            return;

        if (Character.Type == CharacterType.Player && !req.Flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects))
        {
            var stunChance = GetStat(CharacterStat.WhenAttackedStun);
            if (stunChance > 0)
                TryStunTarget(attacker, stunChance);
            var poisonChance = GetStat(CharacterStat.WhenAttackedPoison);
            if (poisonChance > 0)
                TryPoisonOnTarget(attacker, poisonChance);
            var blindChance = GetStat(CharacterStat.WhenAttackedBlind);
            if (blindChance > 0)
                TryBlindTarget(attacker, blindChance);
            var freezeChance = GetStat(CharacterStat.WhenAttackedFreeze);
            if (freezeChance > 0)
                TryFreezeTarget(attacker, freezeChance);
            var sleepChance = GetStat(CharacterStat.WhenAttackedSleep);
            if (sleepChance > 0)
                TrySleepTarget(attacker, sleepChance);
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
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Poison))
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
        if (target.HasBodyState(BodyStateFlags.DisablingState) || target.HasStatusEffectOfType(CharacterStatusEffect.Blind))
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

    public bool TryCurseTarget(CombatEntity target, int chanceIn1000, float delayApply = 0.3f)
    {
        if (target.HasBodyState(BodyStateFlags.DisablingState) || target.HasStatusEffectOfType(CharacterStatusEffect.Curse) || target.GetSpecialType() == CharacterSpecialType.Boss)
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
}
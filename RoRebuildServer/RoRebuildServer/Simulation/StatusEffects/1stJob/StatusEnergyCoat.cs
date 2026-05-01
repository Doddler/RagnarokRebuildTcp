using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.EnergyCoat, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class StatusEnergyCoat : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnCalculateDamageTaken;

    public override StatusUpdateResult OnCalculateDamage(CombatEntity ch, ref StatusEffectState state, ref AttackRequest req, ref DamageInfo info)
    {
        if ((req.Flags & AttackFlags.Physical) == 0 || !info.IsDamageResult || info.Damage <= 0 || ch.Character.Type != CharacterType.Player)
            return StatusUpdateResult.Continue;

        var curSp = ch.GetStat(CharacterStat.Sp);
        var maxSp = ch.GetStat(CharacterStat.MaxSp);

        if (curSp == 0)
            return StatusUpdateResult.EndStatus;

        var shouldEndStatus = false;
        var reduction = 0.15f; //default
        var spCost = 0;

        var percent = float.Clamp(curSp / (float)maxSp, 0.2f, 1f);
        reduction = percent * 0.3f;

        spCost = (int)(maxSp * percent * 0.03f);
        if (spCost < 0)
            spCost = 1;
        if (spCost < maxSp / 100)
            spCost = maxSp / 100;
        if (spCost > curSp)
            shouldEndStatus = true;
        ch.Player.TakeSpValue(spCost);

        //var origDamage = info.Damage;

        info.Damage = int.Clamp((int)(info.Damage * (1 - reduction)), 1, info.Damage);

        //ServerLogger.Log($"Energy Coat activation! Reduction of {reduction} from {origDamage} to {info.Damage} at the cost of {spCost}sp.");

        return shouldEndStatus ? StatusUpdateResult.EndStatus : StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
    }
}
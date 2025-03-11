using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.Smoking, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class SmokingStatus : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage | StatusUpdateMode.OnUpdate;

    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        state.Value1++;

        if (state.Value1 <= 1)
            return StatusUpdateResult.Continue;

        var di = DamageInfo.EmptyResult(ch.Entity, ch.Entity);
        di.AttackSkill = CharacterSkill.Smoking;
        di.Result = AttackResult.NormalDamage;
        di.Damage = 3;
        di.HitCount = 1;
        di.AttackMotionTime = 0.75f;
        di.Time = Time.DeltaTimeFloat + 0.75f;
        di.Flags = DamageApplicationFlags.NoHitLock | DamageApplicationFlags.SkipOnHitTriggers;

        ch.ExecuteCombatResult(di, false, false);
        
        ch.Character.Map?.AddVisiblePlayersAsPacketRecipients(ch.Character);
        CommandBuilder.AttackMulti(null, ch.Character, di, false); //make the client see no attacker
        CommandBuilder.ClearRecipients();
        
        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult && info.AttackSkill != CharacterSkill.Smoking)
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Monster)
            return;

        ch.AddDisabledState();
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Monster)
            return;

        ch.SubDisabledState();
    }
}
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.PecoRiding, StatusClientVisibility.Owner, StatusEffectFlags.NoSave | StatusEffectFlags.StayOnClear)]
public class StatusPecoRiding : StatusEffectBase
{
    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type == CharacterType.Player)
            ch.Player.StopRidingMount();
    }
}
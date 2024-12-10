using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.Pneuma, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
public class StatusPneuma : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnMove;
    
    public override StatusUpdateResult OnMove(CombatEntity ch, ref StatusEffectState state, Position src, Position dest)
    {
        var map = ch.Character.Map;
        if (map != null && map.TryGetAreaOfEffectAtPosition(dest, CharacterSkill.Pneuma, out var effect))
        {
            state.Expiration = effect.Expiration; //we might have moved into a new pneuma, so update
            return StatusUpdateResult.Continue;
        }

        return StatusUpdateResult.EndStatus;
    }
}
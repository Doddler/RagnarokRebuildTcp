using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.StatusEffects.Setup;

public class StatusEffectHandlerAttribute(CharacterStatusEffect statusType, StatusClientVisibility visibility, 
    StatusEffectFlags flags = StatusEffectFlags.None)
    : Attribute
{
    public CharacterStatusEffect StatusType { get; } = statusType;
    public StatusEffectFlags Flags { get; } = flags;
    public StatusClientVisibility VisibilityMode { get; } = visibility;
}
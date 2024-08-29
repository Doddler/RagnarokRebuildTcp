using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.StatusEffects.Setup;

public class StatusEffectHandlerAttribute(CharacterStatusEffect statusType,
    StatusClientVisibility visibility)
    : Attribute
{
    public CharacterStatusEffect StatusType { get; } = statusType;
    public StatusClientVisibility VisibilityMode { get; } = visibility;
}
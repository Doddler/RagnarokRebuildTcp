using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.StatusEffects.Setup;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class StatusEffectHandlerAttribute(
    CharacterStatusEffect statusType,
    StatusClientVisibility visibility,
    StatusEffectFlags flags = StatusEffectFlags.None,
    string shareGroup = "")
    : Attribute
{
    public CharacterStatusEffect StatusType { get; } = statusType;
    public StatusEffectFlags Flags { get; } = flags;
    public StatusClientVisibility VisibilityMode { get; } = visibility;
    public string ShareGroup { get; } = shareGroup;
}
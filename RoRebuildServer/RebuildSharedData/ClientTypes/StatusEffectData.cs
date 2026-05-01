using RebuildSharedData.Enum;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace RebuildSharedData.ClientTypes;

[Serializable]
public class StatusEffectData
{
    public CharacterStatusEffect StatusEffect;
    public string Name;
    public string Description;
    public string Type;
    public string Icon;
    public bool CanDispel;
    public bool CanDisable;
}
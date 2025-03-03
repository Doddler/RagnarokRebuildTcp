namespace RoRebuildServer.EntityComponents.Util;

[Flags]
public enum StatusCleanseTarget
{
    None = 0,
    Poison = 1,
    Silence = 2,
    Blind = 4,
    Confusion = 8,
    Hallucination = 16,
    Curse = 32,
}
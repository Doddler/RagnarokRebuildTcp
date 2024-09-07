namespace RoRebuildServer.EntityComponents.Character
{
    [Flags]
    public enum BodyStateFlags : uint
    {
        None = 0,
        Stopped = 1,
        Curse = 2,
        Blind = 4,
    }
}

namespace RoRebuildServer.EntityComponents.Character
{
    [Flags]
    public enum BodyStateFlags : uint
    {
        None = 0,
        Stopped = 1,
        Curse = 2,
        Frozen = 4,
        Petrified = 8,
        Hidden = 16,
        Blind = 32,
        Sleep = 64,
        Pacification = 128,
        Hallucination = 256,
        Stunned = 512,
        DisablingState = Stunned | Frozen | Petrified | Sleep,
    }
}

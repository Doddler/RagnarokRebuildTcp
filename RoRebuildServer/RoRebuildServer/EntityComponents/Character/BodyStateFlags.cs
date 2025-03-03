namespace RoRebuildServer.EntityComponents.Character
{
    [Flags]
    public enum BodyStateFlags : uint
    {
        None = 0,
        Stopped = 1 << 0,
        Curse = 1 << 1,
        Frozen = 1 << 2,
        Petrified = 1 << 3,
        Hidden = 1 << 4,
        Blind = 1 << 5,
        Sleep = 1 << 6,
        Pacification = 1 << 7,
        Hallucination = 1 << 8,
        Stunned = 1 << 9,
        Cloaking = 1 << 10,
        Silence = 1 << 11,
        Confusion = 1 << 12,

        AnyHiddenState = Hidden | Cloaking,
        DisablingState = Stunned | Frozen | Petrified | Sleep,
        NoAutoAttack = DisablingState | Hidden | Pacification,
        NoSkillAttack = Silence | DisablingState | Pacification,
    }
}

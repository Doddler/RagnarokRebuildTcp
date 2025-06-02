using System.Runtime.CompilerServices;

namespace RoRebuildServer.EntityComponents.Character;

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


//note: The order of these is important, they must match the order of the matching status effect triggers in CharacterStat
[Flags]
public enum StatusTriggerFlags : uint
{
    None = 0,
    Blind = 1 << 0,
    Silence = 1 << 1,
    Curse = 1 << 2,
    Poison = 1 << 3,
    Confusion = 1 << 4,
    HeavyPoison = 1 << 5,
    Bleeding = 1 << 6,
    Stun = 1 << 7,
    Stone = 1 << 8,
    Freeze = 1 << 9,
    Sleep = 1 << 10,

}
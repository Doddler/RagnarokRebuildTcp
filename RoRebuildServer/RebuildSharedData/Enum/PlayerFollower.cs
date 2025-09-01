namespace RebuildSharedData.Enum;

[Flags]
public enum PlayerFollower
{
    None = 0,
    Cart0 = 1 << 1,
    Cart1 = 1 << 2,
    Cart2 = 1 << 3,
    Cart3 = 1 << 4,
    Cart4 = 1 << 5,
    Falcon = 1 << 6,
    AnyCart = Cart0 | Cart1 | Cart2 | Cart3 | Cart4,
    Remove = -1
}

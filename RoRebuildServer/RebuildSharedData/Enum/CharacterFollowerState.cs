namespace RebuildSharedData.Enum;

[Flags]
public enum CharacterFollowerState
{
    None = 0,
    Cart0 = 1 << 1,
    Cart1 = 1 << 2,
    Cart2 = 1 << 3,
    Cart3 = 1 << 4,
    Cart4 = 1 << 5,
    Falcon = 1 << 6,
    AnyCart = Cart0 | Cart1 | Cart2 | Cart3 | Cart4,
    Mounted = 1 << 7,
    Remove = -1
}

public static class CharacterFollowerStateExtensions
{
    //the base level each cart style unlocks at
    private static readonly int[] cartStyleMinLevels = { 0, 41, 66, 81, 91 };

    public static int CartStyleCount => cartStyleMinLevels.Length;

    public static int CartStyleMinLevel(int style) => cartStyleMinLevels[style];

    public static int BestCartStyleForLevel(int level)
    {
        var style = 0;
        while (style + 1 < cartStyleMinLevels.Length && level >= cartStyleMinLevels[style + 1])
            style++;
        return style;
    }

    //the cart flags are consecutive bits, Cart0 to Cart4
    public static CharacterFollowerState CartForStyle(int style) => (CharacterFollowerState)((int)CharacterFollowerState.Cart0 << style);

    //0-4, or -1 without a cart
    public static int CartStyle(this CharacterFollowerState follower)
    {
        var cart = (int)(follower & CharacterFollowerState.AnyCart);
        for (var style = 0; style < cartStyleMinLevels.Length; style++)
            if ((cart & ((int)CharacterFollowerState.Cart0 << style)) != 0)
                return style;
        return -1;
    }
}

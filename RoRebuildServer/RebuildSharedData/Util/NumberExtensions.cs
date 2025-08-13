namespace RebuildSharedData.Util;

public static class NumberExtensions
{
    public static ulong RotateRight(this ulong value, int count)
    {
        return (value >> count) | (value << (32 - count));
    }


    public static long RotateRight(this long value, int count)
    {
        var v2 = (ulong)value;
        return (long)v2.RotateRight(count);
    }
}
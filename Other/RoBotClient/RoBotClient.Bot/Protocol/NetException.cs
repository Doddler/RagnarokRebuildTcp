namespace Lidgren.Network;

/// <summary>
/// Minimal stand-in for Lidgren's NetException, providing the single Assert overload
/// that the verbatim-copied <see cref="NetBitWriter"/> depends on.
/// </summary>
internal static class NetException
{
    public static void Assert(bool isOk, string message)
    {
        if (!isOk)
            throw new Exception(message);
    }
}

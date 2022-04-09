using System.Runtime.InteropServices;

namespace Lidgren.Network;

[StructLayout(LayoutKind.Explicit)]
public struct SingleUIntUnion
{
    /// <summary>
    /// Value as a 32 bit float
    /// </summary>
    [FieldOffset(0)]
    public float SingleValue;

    /// <summary>
    /// Value as an unsigned 32 bit integer
    /// </summary>
    [FieldOffset(0)]
    //[CLSCompliant(false)]
    public uint UIntValue;
}
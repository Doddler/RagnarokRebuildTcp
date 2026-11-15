using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoBotClient.Bot.Protocol;

namespace RoBotClient.Tests;

/// <summary>
/// Phase 0 gate. Golden-vector tests pin the exact bytes the bit-packed codec must produce
/// (little-endian, bool = 1 bit), independently validating the wire format. Round-trip tests
/// confirm writer/reader symmetry, including interleaved fields that force unaligned reads.
/// </summary>
public class ProtocolCodecTests
{
    // ---------- Golden vectors: exact wire bytes ----------

    [Fact]
    public void Byte_WritesSingleByte()
    {
        var w = new PacketWriter();
        w.Write((byte)0xAB);
        Assert.Equal(new byte[] { 0xAB }, w.ToArray());
    }

    [Fact]
    public void Int32_IsLittleEndian()
    {
        var w = new PacketWriter();
        w.Write(0x01020304);
        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, w.ToArray());
    }

    [Fact]
    public void Int16_IsLittleEndian()
    {
        var w = new PacketWriter();
        w.Write((short)0x0102);
        Assert.Equal(new byte[] { 0x02, 0x01 }, w.ToArray());
    }

    [Fact]
    public void Bool_IsSingleBit()
    {
        var w = new PacketWriter();
        w.Write(true);
        Assert.Equal(1, w.Length); // 1 bit rounds up to 1 byte
        Assert.Equal(new byte[] { 0x01 }, w.ToArray());
    }

    [Fact]
    public void BoolThenByte_PacksAcrossBitBoundary()
    {
        // The headline gotcha: a bool consumes 1 bit, so the following byte straddles two bytes.
        var w = new PacketWriter();
        w.Write(true);
        w.Write((byte)0xFF);
        Assert.Equal(2, w.Length); // 9 bits -> 2 bytes
        Assert.Equal(new byte[] { 0xFF, 0x01 }, w.ToArray());
    }

    [Fact]
    public void Float_IsIeee754LittleEndian()
    {
        var w = new PacketWriter();
        w.Write(1.0f); // 0x3F800000
        Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F }, w.ToArray());
    }

    [Fact]
    public void String_IsUshortByteLengthThenUtf8()
    {
        var w = new PacketWriter();
        w.Write("Hi");
        Assert.Equal(new byte[] { 0x02, 0x00, 0x48, 0x69 }, w.ToArray());
    }

    [Fact]
    public void EmptyString_IsZeroLengthPrefix()
    {
        var w = new PacketWriter();
        w.Write("");
        Assert.Equal(new byte[] { 0x00, 0x00 }, w.ToArray());
    }

    [Fact]
    public void Position_IsTwoInt16()
    {
        var w = new PacketWriter();
        w.Write(new Position(10, 20));
        Assert.Equal(new byte[] { 0x0A, 0x00, 0x14, 0x00 }, w.ToArray());
    }

    // ---------- Round-trips ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(123456789)]
    public void Int32_RoundTrips(int value)
    {
        var w = new PacketWriter();
        w.Write(value);
        var r = new PacketReader(w.ToArray(), w.Length);
        Assert.Equal(value, r.ReadInt32());
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)-1)]
    [InlineData(short.MaxValue)]
    [InlineData(short.MinValue)]
    public void Int16_RoundTrips(short value)
    {
        var w = new PacketWriter();
        w.Write(value);
        var r = new PacketReader(w.ToArray(), w.Length);
        Assert.Equal(value, r.ReadInt16());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-1.5f)]
    [InlineData(3.14159f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    public void Float_RoundTrips(float value)
    {
        var w = new PacketWriter();
        w.Write(value);
        var r = new PacketReader(w.ToArray(), w.Length);
        Assert.Equal(value, r.ReadFloat());
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("Ragnarok_Bot_01")]
    [InlineData("Ragnarök")]   // multi-byte UTF-8
    [InlineData("日本語")]
    public void String_RoundTrips(string value)
    {
        var w = new PacketWriter();
        w.Write(value);
        var r = new PacketReader(w.ToArray(), w.Length);
        Assert.Equal(value, r.ReadString());
    }

    [Fact]
    public void ByteAndSByte_RoundTrip()
    {
        var w = new PacketWriter();
        w.Write((byte)200);
        w.Write((sbyte)-50);
        var r = new PacketReader(w.ToArray(), w.Length);
        Assert.Equal((byte)200, r.ReadByte());
        Assert.Equal((sbyte)-50, r.ReadSByte());
    }

    [Fact]
    public void BoolSequence_RoundTrips()
    {
        var pattern = new[] { true, false, true, true, false, false, false, true, true, false, true };
        var w = new PacketWriter();
        foreach (var b in pattern) w.Write(b);
        var r = new PacketReader(w.ToArray(), w.Length);
        foreach (var expected in pattern) Assert.Equal(expected, r.ReadBoolean());
    }

    [Fact]
    public void MixedInterleavedFields_RoundTrip_StressesBitAlignment()
    {
        // Interleave 1-bit bools with multi-byte values so nothing after the first bool is byte-aligned.
        var w = new PacketWriter();
        w.WritePacketType(PacketType.StartWalk);
        w.Write(true);
        w.Write(0x0A0B0C0D);
        w.Write(false);
        w.Write((short)-1234);
        w.Write(true);
        w.Write("mixed");
        w.Write(2.5f);
        w.Write(new Position(120, 250));
        w.Write((byte)0x7E);

        var r = new PacketReader(w.ToArray(), w.Length);
        Assert.Equal(PacketType.StartWalk, r.ReadPacketType());
        Assert.True(r.ReadBoolean());
        Assert.Equal(0x0A0B0C0D, r.ReadInt32());
        Assert.False(r.ReadBoolean());
        Assert.Equal((short)-1234, r.ReadInt16());
        Assert.True(r.ReadBoolean());
        Assert.Equal("mixed", r.ReadString());
        Assert.Equal(2.5f, r.ReadFloat());
        var p = r.ReadPosition();
        Assert.Equal(120, p.X);
        Assert.Equal(250, p.Y);
        Assert.Equal((byte)0x7E, r.ReadByte());
        // Exact bits consumed: 8+1+32+1+16+(16+40)+32+32+8 = 187 (the rest of the final byte is padding).
        Assert.Equal(187, r.Position);
    }
}

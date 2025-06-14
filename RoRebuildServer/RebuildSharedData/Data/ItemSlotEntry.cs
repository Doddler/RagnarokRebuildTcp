using RebuildSharedData.Util;

namespace RebuildSharedData.Data;

public interface ISerializableItem
{
    public void Serialize(IBinaryMessageWriter bw);
}

public unsafe struct RegularItem : ISerializableItem
{
    public int Id;
    public short Count;

    public static int Size => 6;

    public void Serialize(IBinaryMessageWriter bw)
    {
        bw.Write(Id);
        bw.Write(Count);
    }

    public static RegularItem Deserialize(IBinaryMessageReader br)
    {
        var entry = new RegularItem()
        {
            Id = br.ReadInt32(),
            Count = br.ReadInt16()
        };

        return entry;
    }

    public static RegularItem ZeroResult() => new RegularItem() { Id = -1, Count = 0 };
}

[Flags]
public enum UniqueItemFlags : byte
{
    None = 0,
    CraftedItem = 1 << 0,
}

public unsafe struct UniqueItem : ISerializableItem
{
    public int Id;
    public short Count;
    public byte Flags;
    public byte Refine;
    public Guid UniqueId;
    public fixed int Data[4];

    public static int Size => 40; //Id(4) + Count(2) + Flags(2) + UniqueId(16) + Data(4 * 4)

    public int SlotData(int slot) => Data[slot];
    public int SetSlotData(int slot, int val) => Data[slot] = val;

    public void Serialize(IBinaryMessageWriter msg)
    {
        msg.Write(Id);
        msg.Write(Count);
        msg.Write(Flags);
        msg.Write(Refine);
        msg.Write(UniqueId.ToByteArray());
        for (var i = 0; i < 4; i++)
            msg.Write(Data[i]);
    }

    public void SerializeAsRegularItem(IBinaryMessageWriter msg)
    {
        msg.Write(Id);
        msg.Write(Count);
    }

    public static UniqueItem Deserialize(IBinaryMessageReader br)
    {
        var entry = new UniqueItem()
        {
            Id = br.ReadInt32(),
            Count = br.ReadInt16(),
            Flags = br.ReadByte(),
            Refine = br.ReadByte(),
            UniqueId = new Guid(br.ReadBytes(16)),
        };

        for (var i = 0; i < 4; i++)
            entry.Data[i] = br.ReadInt32();

        return entry;
    }
}
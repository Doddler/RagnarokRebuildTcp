namespace RoRebuildServer.EntityComponents.Items;

//public interface ISerializableItem
//{
//    public void Serialize(IBinaryMessageWriter bw);
//}

//public unsafe struct RegularItem : ISerializableItem
//{
//    public int Id;
//    public short Count;

//    public static int Size => 6;

//    public void Serialize(IBinaryMessageWriter bw)
//    {
//        bw.Write(Id);
//        bw.Write(Count);
//    }

//    public static RegularItem Deserialize(BinaryReader br)
//    {
//        var entry = new RegularItem()
//        {
//            Id = br.ReadInt32(),
//            Count = br.ReadInt16()
//        };

//        return entry;
//    }

//    public static RegularItem ZeroResult() => new RegularItem() { Id = -1, Count = 0 };
//}

//public unsafe struct UniqueItem : ISerializableItem
//{
//    public int Id;
//    public short Count;
//    public short Flags;
//    public Guid UniqueId;
//    public fixed int Data[4];

//    public static int Size => 32; //Id(4) + Count(2) + Flags(2) + UniqueId(8) + Data(4 * 4)

//    public void Serialize(IBinaryMessageWriter msg)
//    {
//        msg.Write(Id);
//        msg.Write(Flags);
//        msg.Write(UniqueId.ToByteArray());
//        for (var i = 0; i < 4; i++)
//            msg.Write(Data[i]);
//    }

//    public static UniqueItem Deserialize(BinaryReader br)
//    {
//        var entry = new UniqueItem()
//        {
//            Id = br.ReadInt32(),
//            Count = br.ReadInt16(),
//            Flags = br.ReadInt16(),
//            UniqueId = new Guid(br.ReadBytes(8)),
//        };

//        for (var i = 0; i < 4; i++)
//            entry.Data[i] = br.ReadInt32();

//        return entry;
//    }
//}
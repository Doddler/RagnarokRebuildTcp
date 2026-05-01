using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildZoneServer.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Items;

public struct GroundItem : IEquatable<GroundItem>
{
    public int Id = -1;
    public int ContributorId = -1;
    public float ExclusiveTime;
    public float Expiration;
    public ItemType Type;
    public FloatPosition Position;
    public RegularItem Item;
    public UniqueItem UniqueItem;

    public GroundItem(Position tile, int id, int count)
    {
        var data = DataManager.ItemList[id];
        if (!data.IsUnique)
            InitializeRegularItem(tile, new RegularItem() { Id = id, Count = (short)count });
        else
            InitializeUniqueItem(tile, new UniqueItem() { Id = id, Count = (short)count, UniqueId = Guid.NewGuid() });
    }

    public GroundItem(Position tile, ref ItemReference item)
    {
        if (item.Type == ItemType.RegularItem)
            InitializeRegularItem(tile, item.Item);
        else
            InitializeUniqueItem(tile, item.UniqueItem);
    }

    public GroundItem(Position tile, RegularItem item) => InitializeRegularItem(tile, item);

    public GroundItem(Position tile, UniqueItem item) => InitializeUniqueItem(tile, item);

    public void SetExclusivePickupTime(WorldObject src, float expiration)
    {
        ContributorId = src.Id;
        ExclusiveTime = Time.ElapsedTimeFloat + expiration;
    }

    public void InitializeUniqueItem(Position tile, UniqueItem item)
    {
        Debug.Assert(item.Id > 0);
        Debug.Assert(item.Count > 0);
        Id = World.Instance.GetNextDropId();
        Position = new FloatPosition(tile.X + GameRandom.NextFloat(0.1f, 0.9f), tile.Y + GameRandom.NextFloat(0.1f, 0.9f));
        Type = ItemType.UniqueItem;
        UniqueItem = item;
        Expiration = Time.ElapsedTimeFloat + 60f;
    }

    private void InitializeRegularItem(Position tile, RegularItem item)
    {
        Debug.Assert(item.Id > 0);
        Debug.Assert(item.Count > 0);
        Id = World.Instance.GetNextDropId();
        Position = new FloatPosition(tile.X + GameRandom.NextFloat(0.1f, 0.9f), tile.Y + GameRandom.NextFloat(0.1f, 0.9f));
        Type = ItemType.RegularItem;
        Item = item;
        Expiration = Time.ElapsedTimeFloat + 60f;
    }

    public ItemReference ToItemReference()
    {
#if DEBUG
        if (Type == ItemType.RegularItem)
            Debug.Assert(Item.Id != 0 && Item.Count != 0);
        if (Type == ItemType.UniqueItem)
            Debug.Assert(UniqueItem.Id != 0 && UniqueItem.Count != 0);
#endif

        return new ItemReference() { Type = Type, Item = Item, UniqueItem = UniqueItem };
    }

    public void Serialize(OutboundMessage msg)
    {
        Debug.Assert(Id != -1);

        msg.Write(Id);
        msg.Write(Position.X);
        msg.Write(Position.Y);
        if (Type == ItemType.RegularItem)
        {
            Item.Serialize(msg);
        }
        else
            UniqueItem.SerializeAsRegularItem(msg);
    }

    public bool Equals(GroundItem other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is GroundItem other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public static bool operator ==(GroundItem left, GroundItem right) => left.Equals(right);

    public static bool operator !=(GroundItem left, GroundItem right) => !left.Equals(right);
}
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;

namespace RoRebuildServer.EntityComponents.Items
{
    public struct ItemReference
    {
        public ItemType Type;
        public RegularItem Item;
        public UniqueItem UniqueItem;

        public int Count
        {
            get => Type switch
            {
                ItemType.RegularItem => Item.Count,
                ItemType.UniqueItem => UniqueItem.Count,
                _ => 0
            };
            set
            {
                switch (Type)
                {
                    case ItemType.RegularItem:
                        Item.Count = (short) value;
                        break;
                    case ItemType.UniqueItem:
                        UniqueItem.Count = (short) value;
                        break;
                    default:
                        throw new Exception($"Cannot increase count on an ItemReference as it is not a valid item reference (type = none)");
                }
            }
        }

    public int Id => Type switch
        {
            ItemType.RegularItem => Item.Id,
            ItemType.UniqueItem => UniqueItem.Id,
            _ => -1
        };

        public ItemReference(RegularItem item)
        {
            Type = ItemType.RegularItem;
            Item = item;
        }

        public ItemReference(UniqueItem item)
        {
            Type = ItemType.UniqueItem;
            UniqueItem = item;
        }

        public void Serialize(IBinaryMessageWriter msg, bool asRegularItem = false)
        {
            if (Type == ItemType.RegularItem)
                Item.Serialize(msg);
            else
            {
                if(asRegularItem)
                    UniqueItem.SerializeAsRegularItem(msg);
                else
                    UniqueItem.Serialize(msg);
            }

        }

        public override string ToString() => Type switch
        {
            ItemType.RegularItem => $"[Item:{Item.Count}x {Item.Id}]",
            ItemType.UniqueItem => $"[Item:{UniqueItem.Count}x {UniqueItem.Id} : {UniqueItem.UniqueId}]",
            _ => $"[Empty Item Reference]"
        };
    }
}

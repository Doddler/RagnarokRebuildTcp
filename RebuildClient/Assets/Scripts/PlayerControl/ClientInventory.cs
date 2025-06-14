using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.PlayerControl
{
    public struct InventoryItem : IEquatable<InventoryItem>
    {
        public int BagSlotId;
        public ItemData ItemData;
        public ItemType Type;
        public RegularItem Item;
        public UniqueItem UniqueItem;
        
        public int Id => Type == ItemType.RegularItem ? Item.Id : UniqueItem.Id;

        public InventoryItem(RegularItem item)
        {
            BagSlotId = item.Id;
            Type = ItemType.RegularItem;
            Item = item;
            ItemData = ClientDataLoader.Instance.GetItemById(item.Id);
            UniqueItem = default;
        }
        
        public InventoryItem(int bagId, UniqueItem item)
        {
            BagSlotId = bagId;
            Type = ItemType.RegularItem;
            Item = default;
            ItemData = ClientDataLoader.Instance.GetItemById(item.Id);
            UniqueItem = item;
        }

        public int Count
        {
            get => Type == ItemType.RegularItem ? Item.Count : UniqueItem.Count;
            set
            {
                if (Type == ItemType.RegularItem) 
                    Item.Count = (short)value; 
                else
                    UniqueItem.Count = (short)value; 
            }
        }

        public int SalePrice => ItemData.SellPrice;

        public bool IsAvailableForSocketing(EquipPosition position)
        {
            if (Type != ItemType.UniqueItem)
                return false;

            if (ItemData.ItemClass != ItemClass.Equipment && ItemData.ItemClass != ItemClass.Weapon)
                return false;

            if ((ItemData.Position & position) == 0)
                return false;
            
            var socketCount = ItemData.Slots;
            
            for(var i = 0; i < socketCount; i++)
                if (UniqueItem.SlotData(i) == 0)
                    return true;

            return false;
        }

        public static InventoryItem DeserializeWithType(ClientInboundMessage msg, int bagId)
        {
            var type = (ItemType)msg.ReadByte();
            if (type == ItemType.RegularItem)
                return DeserializeRegular(msg);
            else
                return DeserializeUnique(msg, bagId);
        }
        
        public static InventoryItem Deserialize(ClientInboundMessage msg, ItemType type, int bagId)
        {
            if (type == ItemType.RegularItem)
                return DeserializeRegular(msg);
            else
                return DeserializeUnique(msg, bagId);
        }

        public static InventoryItem DeserializeRegular(ClientInboundMessage message)
        {
            var item = RegularItem.Deserialize(message);
            
            return new InventoryItem(item);
        }

        public static InventoryItem DeserializeUnique(ClientInboundMessage message, int bagSlotId)
        {
            var item = new InventoryItem
            {
                Type = ItemType.UniqueItem,
                UniqueItem = UniqueItem.Deserialize(message)
            };
            item.BagSlotId = bagSlotId;
            item.ItemData = ClientDataLoader.Instance.GetItemById(item.UniqueItem.Id);
            return item;
        }
        
        private static StringBuilder sb = new();
        private static CardPrefixData[] prefixData = new CardPrefixData[4];

        public string MakeNameWithSockets() => MakeNameWithSockets(UniqueItem, ItemData);

        //there must be a non-retarded way to do this that doesn't allocate like a hog
        public static string MakeNameWithSockets(UniqueItem uniqueItem, ItemData data)
        {
            Span<int> ids = stackalloc int[4];
            Span<int> counts = stackalloc int[4];
            var uniqueSlot = 0;
            for (var i = 0; i < 4; i++)
            {
                var item = uniqueItem.SlotData(i);
                if (item > 0)
                {
                    var hasMatch = false;
                    for (var j = 0; j < 4; j++)
                    {
                        if (ids[j] == item)
                        {
                            counts[j]++;
                            hasMatch = true;
                            break;
                        }
                    }

                    if (!hasMatch)
                    {
                        ids[uniqueSlot] = item;
                        counts[uniqueSlot] = 1;
                        prefixData[uniqueSlot] = ClientDataLoader.Instance.GetCardPrefixData(item);
                        uniqueSlot++;
                    }
                }
            }

            if (uniqueSlot == 0)
                return data.Name;

            sb.Clear();
            //prefixes
            for (var i = 0; i < uniqueSlot; i++)
            {
                if (prefixData[i] != null && !string.IsNullOrWhiteSpace(prefixData[i].Prefix))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    switch(counts[i])
                    {
                        default: sb.Append(prefixData[i].Prefix); break;
                        case 2: sb.Append("Double ").Append(prefixData[i].Prefix); break;
                        case 3: sb.Append("Triple ").Append(prefixData[i].Prefix); break;
                        case 4: sb.Append("Quadruple ").Append(prefixData[i].Prefix); break;
                    }
                }
            }

            if (sb.Length > 0)
                sb.Append(" ");
            sb.Append(data.Name);
            
            //postfixes
            for (var i = 0; i < uniqueSlot; i++)
            {
                if (prefixData[i] != null && !string.IsNullOrWhiteSpace(prefixData[i].Postfix))
                {
                    sb.Append(" ");
                    switch (counts[i])
                    {
                        default: sb.Append(prefixData[i].Postfix); break;
                        case 2: sb.Append(prefixData[i].Postfix).Append("Double "); break;
                        case 3: sb.Append(prefixData[i].Postfix).Append("Triple "); break;
                        case 4: sb.Append(prefixData[i].Postfix).Append("Quadruple "); break;
                    }
                }
            }

            return sb.ToString();
        }
        
        public bool Equals(InventoryItem other)
        {
            return BagSlotId == other.BagSlotId;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return BagSlotId;
        }

        public string ProperName()
        {
            if (Type == ItemType.UniqueItem)
                return MakeProperName(UniqueItem, ItemData);
            else
                return MakeProperName(Item, ItemData);
        }

        public static string MakeProperName(RegularItem item, ItemData data)
        {
            if(data.Slots > 0)
                return $"{data.Name}[{data.Slots}]";
            return data.Name;
        }
        
        public static string MakeProperName(UniqueItem item, ItemData data)
        {
            if (data == null)
                return $"Unknown Item";
            
            if (data.IsUnique)
            {
                var refine = "";
                if (item.Refine > 0)
                    refine = $"+{item.Refine} ";
                if (data.Slots == 0 || (item.Flags & (byte)UniqueItemFlags.CraftedItem) > 0)
                {
                    if (item.SlotData(0) == 0)
                        return $"{refine}{data.Name}";
                    return $"{refine}{MakeNameWithSockets(item, data)}";
                }

                return $"{refine}{MakeNameWithSockets(item, data)}[{data.Slots}]";
            }

            return $"{data.Name}";
        }

        public override string ToString()
        {
            if (ItemData == null)
                return $"Unknown Item";
            
            if (ItemData.IsUnique)
            {
                var refine = "";
                if (UniqueItem.Refine > 0)
                    refine = $"+{UniqueItem.Refine} ";
                if (ItemData.Slots == 0)
                    return $"{refine}{ItemData.Name}";
                return $"{refine}{MakeNameWithSockets()}[{ItemData.Slots}]";
            }

            return $"{ItemData.Name}: {Count} ea.";
        }
    }
    
    public class ClientInventory
    {
        private readonly SortedDictionary<int, InventoryItem> inventoryLookup = new();

        public SortedDictionary<int, InventoryItem> GetInventoryData() => inventoryLookup;
        public Dictionary<int, int> UniqueItemCounts = new();
        public InventoryItem GetInventoryItem(int bagId) => inventoryLookup[bagId];
        public bool TryGetInventoryItem(int bagId, out InventoryItem item) => inventoryLookup.TryGetValue(bagId, out item);

        public int TotalItems => inventoryLookup.Count;
        
        public int GetItemCount(int itemId)
        {
            if(inventoryLookup.TryGetValue(itemId, out var item))
                return item.Type == ItemType.RegularItem ? item.Item.Count : item.UniqueItem.Count;
            return 0;
        }

        public int CountItemByItemId(int itemId)
        {
            if (inventoryLookup.TryGetValue(itemId, out var item))
                return item.Count;
            return UniqueItemCounts.GetValueOrDefault(itemId, 0);
        }

        public bool RemoveItem(int itemId, int count)
        {
            if (!inventoryLookup.TryGetValue(itemId, out var item))
                return false;

            if (item.Count <= count || item.Type == ItemType.UniqueItem)
            {
                inventoryLookup.Remove(itemId);
                if (UniqueItemCounts.TryGetValue(item.Id, out var curCount) && curCount - count <= 0)
                    UniqueItemCounts.Remove(item.Id);
                else
                    UniqueItemCounts[item.Id] = curCount - count;

            }
            else
            {
                item.Count -= count;
                inventoryLookup[itemId] = item;
            }

            return true;
        }

        public void UpdateItem(InventoryItem item)
        {
            var change = item.Count;
            if (!inventoryLookup.ContainsKey(item.BagSlotId) && item.Type == ItemType.UniqueItem)
            {
                if (UniqueItemCounts.TryGetValue(item.Id, out var curCount))
                    UniqueItemCounts[item.Id] += 1;
                else
                    UniqueItemCounts[item.Id] = 1;
            }
            
            inventoryLookup[item.BagSlotId] = item;
        }

        public bool TryFindCatalystCapableItem(int baseId, int ignoreBagId, out InventoryItem catalyst)
        {
            foreach (var (_, item) in inventoryLookup)
            {
                if (!item.ItemData.IsUnique || item.Id != baseId || item.BagSlotId == ignoreBagId)
                    continue;

                if (item.UniqueItem.Refine > 0)
                    continue;

                var isSocketed = false;
                for(var i = 0; i < 4; i++)
                    if (item.UniqueItem.SlotData(i) > 0)
                        isSocketed = true;
                
                if (isSocketed)
                    continue;

                catalyst = item;
                return true;
            }
            
            catalyst = default;
            return false;
        }

        public void ReplaceUniqueItem(int bagId, UniqueItem item)
        {
            if (!inventoryLookup.TryGetValue(bagId, out var curItem))
            {
                Debug.LogError($"Could not ReplaceUniqueItem with bagId {bagId} (item {item.Id}), that item could not be found.");
                return;
            }

            curItem.UniqueItem = item;
            inventoryLookup[bagId] = curItem;
        }
        
        public void Deserialize(ClientInboundMessage msg)
        {
            inventoryLookup.Clear();
            UniqueItemCounts.Clear();
            var hasBagData = msg.ReadByte();
            if (hasBagData == 0)
                return;
            var regularCount = msg.ReadInt32();
            for (var i = 0; i < regularCount; i++)
            {
                var item = InventoryItem.DeserializeRegular(msg);
                inventoryLookup.Add(item.Item.Id, item);
                // items.Add(item);
            }
            var uniqueCount = msg.ReadInt32();
            for (var i = 0; i < uniqueCount; i++)
            {
                var key = msg.ReadInt32();
                var item = InventoryItem.DeserializeUnique(msg, key);
                inventoryLookup.Add(key, item);
                if (UniqueItemCounts.TryGetValue(item.Id, out var count))
                    UniqueItemCounts[item.Id] = count + 1;
                else
                    UniqueItemCounts.Add(item.Id, 1);
                // items.Add(item);
            }
        }
    }
}
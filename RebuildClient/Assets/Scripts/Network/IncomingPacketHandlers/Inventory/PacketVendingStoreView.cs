using System.Collections.Generic;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI;
using Assets.Scripts.UI.Inventory;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.VendingViewStore)]
    public class PacketVendingStoreView : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var vendorId = msg.ReadInt32();
            var name = msg.ReadString();
            var itemCount = msg.ReadInt32();

            var items = new Dictionary<int, VendingEntry>(itemCount);

            for (var i = 0; i < itemCount; i++)
            {
                var bagId = msg.ReadInt32();
                var item = InventoryItem.DeserializeWithType(msg, bagId);
                var price = msg.ReadInt32();

                items.Add(bagId, new VendingEntry() { Item = item, Price = price });
            }

            string shopName;
            if (Network.EntityList.TryGetValue(vendorId, out var vendor))
                shopName = $"{vendor.Name}'s Shop: {name}";
            else
                shopName = $"Player Shop: {name}";

            VendingShopViewUI.StartViewVendingShop(shopName, items);
        }
    }
}
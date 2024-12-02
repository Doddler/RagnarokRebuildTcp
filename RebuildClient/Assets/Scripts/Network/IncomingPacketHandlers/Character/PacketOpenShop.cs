using System;
using System.Collections.Generic;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.OpenShop)]
    public class PacketOpenShop : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var shopType = msg.ReadByte();
            if (shopType == 0)
            {
                //sell to npc
                
                var shop = ShopUI.InitializeShopUI(UiManager.GeneralItemListPrefab, UiManager.PrimaryUserWindowContainer);
                shop.BeginSellToNpc();
            }
            else
            {
                //buy from npc
                var count = msg.ReadInt32();
                var items = new List<ShopEntry>(count);
                for (var i = 0; i < count; i++)
                {
                    var id = msg.ReadInt32();
                    var price = msg.ReadInt32();
                    items.Add(new ShopEntry() { ItemId = id, Cost = price, Count = -1 });
                }

                var shop = ShopUI.InitializeShopUI(UiManager.GeneralItemListPrefab, UiManager.PrimaryUserWindowContainer);
                shop.BeginBuyFromNpc(items);
            }
        }
    }
}
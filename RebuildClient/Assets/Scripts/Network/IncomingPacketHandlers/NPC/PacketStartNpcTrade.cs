using System.Collections.Generic;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.StartNpcTrade)]
    public class PacketStartNpcTrade : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var tradeCount = msg.ReadByte();
            var trades = new List<ItemTrade>();
            
            for (var i = 0; i < tradeCount; i++)
            {
                var item = InventoryItem.DeserializeWithType(msg, -1);
                var count = msg.ReadInt32();
                var zenyCost = msg.ReadInt32();
                var reqCount = msg.ReadInt32();
                var reqList = new List<RegularItem>();
                for (var j = 0; j < reqCount; j++)
                    reqList.Add(RegularItem.Deserialize(msg));

                trades.Add(new ItemTrade(item, count, zenyCost, reqList));
            }
            
            Camera.DialogPanel.GetComponent<DialogWindow>().HideUI();
            NpcItemTradingUI.StartNpcTrade(trades);
        }
    }
}
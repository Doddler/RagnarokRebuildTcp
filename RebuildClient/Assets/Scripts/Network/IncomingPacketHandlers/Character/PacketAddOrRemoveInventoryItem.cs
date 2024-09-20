using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.AddOrRemoveInventoryItem)]
    public class PacketAddOrRemoveInventoryItem : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (ItemType)msg.ReadByte();
            var change = msg.ReadInt32();
            if (type == ItemType.RegularItem)
            {
                var item = RegularItem.Deserialize(msg);
                State.Inventory.UpdateItem(item);
                Debug.Log($"Added {change} of item type {item.Id}");
            }
            UiManager.SkillHotbar.UpdateItemCounts();
        }
    }
}
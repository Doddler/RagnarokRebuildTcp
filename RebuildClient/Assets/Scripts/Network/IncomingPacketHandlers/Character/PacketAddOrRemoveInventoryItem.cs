using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
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
            var isAddItem = msg.ReadBoolean();
            if (isAddItem)
            {
                var type = (ItemType)msg.ReadByte();
                var bagId = msg.ReadInt32();
                var change = (int)msg.ReadInt16();
                var item = InventoryItem.Deserialize(msg, type, bagId);
                State.Inventory.UpdateItem(item);
                Debug.Log($"Added {change} of item type {item.Id}");
            }
            else
            {
                var bagId = msg.ReadInt32();
                var change = (int)msg.ReadInt16();
                State.Inventory.RemoveItem(bagId, change);
                Debug.Log($"Removed {change} of item with a bag Id {bagId}");
            }
            UiManager.SkillHotbar.UpdateItemCounts();
            UiManager.InventoryWindow.UpdateActiveVisibleBag();
        }
    }
}
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.AddOrRemoveInventoryItem)]
    public class PacketAddOrRemoveInventoryItem : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var isAddItem = msg.ReadBoolean();
            var oldWeight = State.CurrentWeight;
            if (isAddItem)
            {
                var type = (ItemType)msg.ReadByte();
                var bagId = msg.ReadInt32();
                var change = (int)msg.ReadInt16();
                State.SetWeight(msg.ReadInt32());
                var item = InventoryItem.Deserialize(msg, type, bagId);
                State.Inventory.UpdateItem(item);
                UiManager.Instance.ItemObtainedPopup.SetText(item, change);
                // Debug.Log($"Added {change} of item type {item.Id} (weight changed {State.CurrentWeight - oldWeight} to {State.CurrentWeight})");
            }
            else
            {
                var bagId = msg.ReadInt32();
                var change = (int)msg.ReadInt16();
                State.SetWeight(msg.ReadInt32());

                if(msg.ReadBoolean())
                {
                    var item = State.Inventory.GetInventoryItem(bagId);
                    if(item.Type == ItemType.UniqueItem)
                        Camera.AppendChatText($"<color={TextColors.Unequip}>You lost {item.ProperName()}.</color>");
                    else
                        Camera.AppendChatText($"<color={TextColors.Unequip}>You lost {change}x {item.ProperName()}.</color>");
                        
                }
                
                State.Inventory.RemoveItem(bagId, change);
                
                
                //if(msg.ReadBoolean())
                // Debug.Log($"Removed {change} of item with a bag Id {bagId} (weight changed {State.CurrentWeight - oldWeight} to {State.CurrentWeight})");
            }
        }
    }
}
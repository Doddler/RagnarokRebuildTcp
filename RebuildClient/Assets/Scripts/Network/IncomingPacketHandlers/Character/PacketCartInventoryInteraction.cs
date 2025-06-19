using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.CartInventoryInteraction)]
    public class PacketCartInventoryInteraction : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (CartInteractionType)msg.ReadByte();
            var bagId = msg.ReadInt32();
            var item = InventoryItem.DeserializeWithType(msg, bagId);
            var change = (int)msg.ReadInt16();
            var cartWeight = msg.ReadInt32();
            var otherWeight = msg.ReadInt32();

            PlayerState.Instance.CartWeight = cartWeight;
            
            if (type == CartInteractionType.CartToInventory)
            {
                //item is already added to inventory with another packet, just remove item from cart
                PlayerState.Instance.Cart.RemoveItem(bagId, change);
                PlayerState.Instance.CurrentWeight = otherWeight;
                UiManager.Instance.CartWindow.UpdateActiveVisibleBag();
            }

            if (type == CartInteractionType.InventoryToCart)
            {
                //item should already be removed form the inventory with another placket
                PlayerState.Instance.Cart.UpdateItem(item);
                PlayerState.Instance.CurrentWeight = otherWeight;
                UiManager.Instance.CartWindow.UpdateActiveVisibleBag();
            }
        }
    }
}
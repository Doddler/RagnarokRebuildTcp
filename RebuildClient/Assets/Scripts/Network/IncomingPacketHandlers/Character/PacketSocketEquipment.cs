using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.SocketEquipment)]
    public class PacketSocketEquipment : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var bagId = msg.ReadInt32();
            var updatedItem = UniqueItem.Deserialize(msg);
            
            State.Inventory.ReplaceUniqueItem(bagId, updatedItem);
            
            UiManager.InventoryWindow.UpdateActiveVisibleBag();
            UiManager.EquipmentWindow.RefreshEquipmentWindow();
        }
    }
}
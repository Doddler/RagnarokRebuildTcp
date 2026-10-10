using Assets.Scripts.Network.HandlerBase;
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
            // Don't reveal the refine window here; SubmitRefine reveals it after the effect — doing it now flickers it.
        }
    }
}
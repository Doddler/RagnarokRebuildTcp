using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.EquipUnequipGear)]
    public class PacketEquipUnequipGear : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var bagId = msg.ReadInt32();
            var slot = (int)msg.ReadByte();
            var isEquip = msg.ReadBoolean();
            if (isEquip)
                State.EquippedItems[slot] = bagId;
            else
                State.EquippedItems[slot] = 0;
            UiManager.EquipmentWindow.RefreshEquipmentWindow();
        }
    }
}
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.StorageInteraction)]
    public class PacketStorageInteraction : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (ItemType)msg.ReadByte();
            var bagId = msg.ReadInt32();
            var change = (int)msg.ReadInt16();
            var item = InventoryItem.Deserialize(msg, type, bagId);
            State.SetWeight(msg.ReadInt32());
            var storageCount = msg.ReadInt32();
            var isMoveToStorage = msg.ReadBoolean();

            if (isMoveToStorage)
                State.Storage.UpdateItem(item);
            else
                State.Storage.RemoveItem(bagId, change);
        }
    }
}
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminCreateItem)]
    public class PacketAdminCreateItem : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsOnlineAdmin)
                return;

            var itemId = msg.ReadInt32();
            var count = msg.ReadInt32();
            if (count <= 0 || count > 10000)
                count = 20;

            connection.Player?.CreateItemInInventory(new ItemReference(itemId, count));
        }
    }
}

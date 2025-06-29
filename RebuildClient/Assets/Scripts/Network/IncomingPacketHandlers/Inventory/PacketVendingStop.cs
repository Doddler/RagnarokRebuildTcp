using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI.Inventory;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.VendingStop)]
    public class PacketVendingStop : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            VendingActiveWindow.Instance?.StopVending();
        }
    }
}
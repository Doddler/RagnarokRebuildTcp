using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI.Inventory;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.VendingNotifyOfSale)]
    public class PacketVendingNotifyOfSale : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var bagId = msg.ReadInt32();
            var count = msg.ReadInt32();
            
            VendingActiveWindow.Instance.ReceiveNotificationOfSale(bagId, count);
            PlayerState.Instance.Cart.RemoveItem(bagId, count);
        }
    }
}
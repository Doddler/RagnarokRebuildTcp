using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Party
{
    [ClientPacketHandler(PacketType.InvitePartyMember)]
    public class PacketInvitePartyMember : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var partyId = msg.ReadInt32();
            var partyName = msg.ReadString();
            var senderName = msg.ReadString();

            State.InvitedPartyId = partyId;
            
            UiManager.Instance.ToastNotificationArea.AddPartyInvite(partyId, senderName, partyName);
        }
    }
}
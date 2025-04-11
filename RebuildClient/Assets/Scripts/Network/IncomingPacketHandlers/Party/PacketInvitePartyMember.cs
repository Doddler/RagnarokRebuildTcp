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
            
            Camera.AppendChatText($"<color=#77FF77>{senderName} has invited you to join their party '{partyName}'.\nType /accept to accept the last request ('/accept {partyId}' for this party.)</color>");
        }
    }
}
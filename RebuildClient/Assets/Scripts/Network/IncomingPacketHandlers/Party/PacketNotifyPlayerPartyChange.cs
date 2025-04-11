using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Party
{
    [ClientPacketHandler(PacketType.NotifyPlayerPartyChange)]
    public class PacketNotifyPlayerPartyChange : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var playerId = msg.ReadInt32();

            if (!Network.EntityList.TryGetValue(playerId, out var controllable))
                return;

            var isJoinParty = msg.ReadByte() == 1;

            if (isJoinParty)
            {
                var partyId = msg.ReadInt32();
                var partyName = msg.ReadString();
                var isOwner = msg.ReadBoolean();
                
                controllable.PartyName = partyName;
            }
            else
                controllable.PartyName = null;
        }
    }
}
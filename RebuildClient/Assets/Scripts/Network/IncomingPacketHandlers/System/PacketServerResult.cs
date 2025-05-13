using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI.Utility;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.System
{
    [ClientPacketHandler(PacketType.ServerResult)]
    public class PacketServerResult : ClientPacketHandlerBase
    {   
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (ServerResult)msg.ReadByte();
            var val = msg.ReadInt32();
            // var text = msg.ReadString();

            switch (type)
            {
                case ServerResult.PartyInviteSent:
                    Camera.AppendChatText($"A party invite has been sent.", TextColor.Party);
                    break;
                case ServerResult.InviteFailedAlreadyInParty:
                    Camera.AppendChatText($"Party invite failed, player is already in another party.", TextColor.Error);
                    break;
                case ServerResult.InviteFailedRecipientNoBasicSkill:
                    Camera.AppendChatText($"Party invite failed, player's basic skill level is too low to join.", TextColor.Error);
                    break;
                case ServerResult.InviteFailedSenderNoBasicSkill:
                    Camera.AppendChatText($"You do not have the required basic skill level to join a party.", TextColor.Error);
                    break;
            }
        }
    }
}
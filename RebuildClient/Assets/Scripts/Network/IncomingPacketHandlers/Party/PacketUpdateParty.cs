using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Party
{
    [ClientPacketHandler(PacketType.UpdateParty)]
    public class PacketUpdateParty : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var updateType = (PartyUpdateType)msg.ReadByte();
            switch (updateType)
            {
                case PartyUpdateType.AddPlayer:
                    var newMember = PartyMemberData.Read(msg);
                    State.Party.AddMember(newMember);
                    Camera.AppendChatText($"{newMember.Name} has joined the party.", TextColor.Party);
                    break;
                case PartyUpdateType.LogIn:
                case PartyUpdateType.LogOut:
                case PartyUpdateType.UpdatePlayer:
                    var memberInfo = PartyMemberData.Read(msg);
                    
                    if(updateType == PartyUpdateType.LogIn)
                        Camera.AppendChatText($"{memberInfo.Name} has logged in.", TextColor.Party);
                    if(updateType == PartyUpdateType.LogOut)
                        Camera.AppendChatText($"{memberInfo.Name} has logged out.", TextColor.Party);
                    State.Party.UpdateMember(memberInfo);
                    break;
                case PartyUpdateType.RemovePlayer:
                    var removePartyId = msg.ReadInt32();
                    var existing = State.Party.RemoveMember(removePartyId);
                    if (existing == null)
                        break; //a member this client never had
                    if (existing.EntityId == Network.PlayerId)
                    {
                        updateType = PartyUpdateType.LeaveParty;
                        goto case PartyUpdateType.LeaveParty;
                    }
                    Camera.AppendChatText($"{existing.PlayerName} has left the party.", TextColor.Party);
                    break;
                case PartyUpdateType.ChangeLeader:
                    var newLeaderId = msg.ReadInt32();
                    if (State.Party.ChangeLeader(newLeaderId))
                    {
                        if (newLeaderId == State.Party.MemberId)
                            Camera.AppendChatText($"You have been promoted to party leader.", TextColor.Party);
                        else
                            Camera.AppendChatText($"{State.Party.Members[newLeaderId].PlayerName} has been promoted to party leader.", TextColor.Party);
                    }
                    break;
                case PartyUpdateType.UpdateMap:
                {
                    var memberId = msg.ReadInt32();
                    State.Party.SetMemberMap(memberId, msg.ReadString());
                    break;
                }
                case PartyUpdateType.UpdateHpSp:
                {
                    var memberId = msg.ReadInt32();
                    State.Party.SetMemberVitals(memberId, msg.ReadInt32(), msg.ReadInt32(), msg.ReadInt32(), msg.ReadInt32());
                    break;
                }
                case PartyUpdateType.LeaveParty:
                case PartyUpdateType.DisbandParty:
                    if(updateType == PartyUpdateType.LeaveParty)
                        Camera.AppendChatText($"You have left the party.", TextColor.Party);
                    if(updateType == PartyUpdateType.DisbandParty)
                        Camera.AppendChatText($"The party has disbanded.", TextColor.Party);
                    State.Party.Leave();
                    break;
            }
        }
    }
}
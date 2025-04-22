using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

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
                    var newMember = PartyPacketHelpers.LoadPartyMemberInfo(msg);
                    State.RegisterOrUpdatePartyMember(newMember);
                    Camera.AppendChatText($"<color=#77FF77>{newMember.PlayerName} has joined the party.</color>");
                    UiManager.Instance.PartyPanel.AddPartyMember(newMember);
                    break;
                case PartyUpdateType.LogIn:
                case PartyUpdateType.LogOut:
                case PartyUpdateType.UpdatePlayer:
                    var memberInfo = PartyPacketHelpers.LoadPartyMemberInfo(msg);
                    
                    if(updateType == PartyUpdateType.LogIn)
                        Camera.AppendChatText($"<color=#77FF77>{memberInfo.PlayerName} has logged in.</color>");
                    if(updateType == PartyUpdateType.LogOut)
                        Camera.AppendChatText($"<color=#77FF77>{memberInfo.PlayerName} has logged out.</color>");
                    State.RegisterOrUpdatePartyMember(memberInfo);
                    UiManager.Instance.PartyPanel.RefreshPartyMember(memberInfo.PartyMemberId);
                    break;
                case PartyUpdateType.RemovePlayer:
                    var removePartyId = msg.ReadInt32();
                    var existing = State.RemovePartyMember(removePartyId);
                    if (existing.EntityId == Network.PlayerId)
                    {
                        updateType = PartyUpdateType.LeaveParty;
                        goto case PartyUpdateType.LeaveParty;
                    }
                    UiManager.Instance.PartyPanel.RemovePartyMember(removePartyId);
                    Camera.AppendChatText($"<color=#77FF77>{existing.PlayerName} has left the party.</color>");
                    break;
                case PartyUpdateType.ChangeLeader:
                    var newLeader = msg.ReadInt32();
                    if (State.PartyMembers.TryGetValue(newLeader, out var leader))
                    {
                        State.PartyLeader = newLeader;
                        if (newLeader == State.EntityId)
                            Camera.AppendChatText($"<color=#77FF77>You have been promoted to party leader.</color>");
                        else
                            Camera.AppendChatText($"<color=#77FF77>{leader.PlayerName} has been promoted to party leader.</color>");
                        State.UpdatePlayerName(); //add party leader indicator (or remove it)
                        UiManager.Instance.PartyPanel.RefreshPartyMember(newLeader);
                    }
                    break;
                case PartyUpdateType.UpdateMap:
                    if (State.PartyMembers.TryGetValue(msg.ReadInt32(), out var mapUpdatePlayer))
                    {
                        mapUpdatePlayer.Map = msg.ReadString();
                        UiManager.Instance.PartyPanel.RefreshPartyMember(mapUpdatePlayer.PartyMemberId);
                    }
                    break;
                case PartyUpdateType.UpdateHpSp:
                    if (State.PartyMembers.TryGetValue(msg.ReadInt32(), out var hpUpdatePlayer))
                    {
                        hpUpdatePlayer.Hp = msg.ReadInt32();
                        hpUpdatePlayer.MaxHp = msg.ReadInt32();
                        hpUpdatePlayer.Sp = msg.ReadInt32();
                        hpUpdatePlayer.MaxSp = msg.ReadInt32();
                        UiManager.Instance.PartyPanel.UpdateHpSpOfPartyMember(hpUpdatePlayer.PartyMemberId);
                    }
                    break;
                case PartyUpdateType.LeaveParty:
                case PartyUpdateType.DisbandParty:
                    if(updateType == PartyUpdateType.LeaveParty)
                        Camera.AppendChatText($"<color=#77FF77>You have left the party.</color>");
                    if(updateType == PartyUpdateType.DisbandParty)
                        Camera.AppendChatText($"<color=#77FF77>The party has disbanded.</color>");
                    State.IsInParty = false;
                    State.PartyMembers.Clear();
                    State.PartyMemberEntityLookup.Clear();
                    State.PartyMemberIdLookup.Clear();
                    State.UpdatePlayerName();
                    UiManager.Instance.PartyPanel.FullRefreshPartyMemberPanel();
                    MinimapController.Instance.RefreshPartyMembers();
                    if (Camera.TargetControllable != null)
                        Camera.TargetControllable.PartyName = null;
                    break;
            }
        }
    }
}
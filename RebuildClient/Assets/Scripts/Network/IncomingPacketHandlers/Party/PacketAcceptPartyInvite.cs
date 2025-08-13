using System.Text;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Party
{
    [ClientPacketHandler(PacketType.AcceptPartyInvite)]
    public class PacketAcceptPartyInvite : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var isLogIn = msg.ReadByte() == 1;
            State.IsInParty = true;
            State.PartyId = msg.ReadInt32();
            State.PartyName = msg.ReadString();
            State.PartyMembers.Clear();
            State.PartyMemberEntityLookup.Clear();

            var sb = new StringBuilder();
            
            if(isLogIn)
                sb.AppendLine($"<color=#77FF77>You are in party '{State.PartyName}'</color>");
            else
            {
                sb.AppendLine($"<color=#77FF77>You have joined the party '{State.PartyName}'</color>");
                sb.AppendLine($"<color=#77FF77>Exp gained will be shared with party members within 10 levels.</color>");
            }

            sb.Append($"<color=#77FF77>Party members: ");

            if (CameraFollower.Instance.TargetControllable != null)
            {
                CameraFollower.Instance.TargetControllable.IsPartyMember = true;
                CameraFollower.Instance.TargetControllable.PartyName = State.PartyName;
            }
            
            LoadPartyMemberDetails(msg, sb);

            sb.Append("</color>");
            Camera.AppendChatText(sb.ToString());
            State.UpdatePlayerName(); //add party leader indicator (or remove it)
            UiManager.Instance.PartyPanel.FullRefreshPartyMemberPanel();
        }
        
        public static void LoadPartyMemberDetails(ClientInboundMessage msg, StringBuilder sb = null)
        {
            var partyMemberCount = msg.ReadInt32();
            for (var i = 0; i < partyMemberCount; i++)
            {
                var m = PartyPacketHelpers.LoadPartyMemberInfo(msg);
                
                if (sb != null)
                {
                    if (i > 0)
                        sb.Append(", ");
                    if (m.IsLeader)
                        sb.Append("★");
                    sb.Append(m.PlayerName);
                    if (m.EntityId <= 0)
                        sb.Append($" (offline)");
                }

                if (NetworkManager.Instance.EntityList.TryGetValue(m.EntityId, out var controllable))
                    m.Controllable = controllable;

                PlayerState.Instance.RegisterOrUpdatePartyMember(m);
            }
        }
    }
}
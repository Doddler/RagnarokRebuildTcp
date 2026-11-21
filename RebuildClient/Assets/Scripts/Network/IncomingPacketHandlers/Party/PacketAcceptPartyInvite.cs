using System.Collections.Generic;
using System.Text;
using RebuildSharedData.Data;
using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Party
{
    [ClientPacketHandler(PacketType.AcceptPartyInvite)]
    public class PacketAcceptPartyInvite : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var isLogIn = msg.ReadByte() == 1;
            var partyId = msg.ReadInt32();
            var partyName = msg.ReadString();
            var partyMembers = LoadPartyMembers(msg);
            State.Party.Join(partyId, partyName, partyMembers);

            var sb = new StringBuilder();
            sb.Append(isLogIn ? $"You are in party '{partyName}'. " : $"You have joined the party '{partyName}'. ");
            AppendMemberSummary(sb, partyMembers);

            if (!isLogIn)
                sb.Append("\nExp gained will be shared with party members within 10 levels.");

            Camera.AppendChatText(sb.ToString(), TextColor.Party);
        }

        private static List<PartyMemberData> LoadPartyMembers(ClientInboundMessage msg)
        {
            var partyMemberCount = msg.ReadInt32();
            var partyMembers = new List<PartyMemberData>(partyMemberCount);
            for (var i = 0; i < partyMemberCount; i++)
                partyMembers.Add(PartyMemberData.Read(msg));
            return partyMembers;
        }

        private static void AppendMemberSummary(StringBuilder sb, List<PartyMemberData> partyMembers)
        {
            string leader = null;
            var members = new List<string>();
            foreach (var m in partyMembers)
            {
                var label = !m.IsOnline ? $"{m.Name} (offline)" : m.Name;
                if (m.IsLeader)
                    leader = label;
                else
                    members.Add(label);
            }

            sb.Append($"Lead: {leader}");
            if (members.Count > 0)
                sb.Append($". Members: {string.Join(", ", members)}");
        }
    }
}
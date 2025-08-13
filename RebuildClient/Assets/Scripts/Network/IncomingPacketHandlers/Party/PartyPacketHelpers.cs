using Assets.Scripts.Data;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Party
{
    public static class PartyPacketHelpers
    {
        public static PartyMemberInfo LoadPartyMemberInfo(ClientInboundMessage msg)
        {
            var partyId = msg.ReadInt32();
            var entityId = msg.ReadInt32();
            var level = (int)msg.ReadInt16();
            var playerName = msg.ReadString();
            var isLeader = msg.ReadByte() == 1;


            var partyMember = new PartyMemberInfo()
            {
                PartyMemberId = partyId,
                EntityId = entityId,
                Level = level,
                IsLeader = isLeader,
                PlayerName = playerName
            };

            if (entityId > 0)
            {
                partyMember.Map = msg.ReadString();
                partyMember.Hp = msg.ReadInt32();
                partyMember.MaxHp = msg.ReadInt32();
                partyMember.Sp = msg.ReadInt32();
                partyMember.MaxSp = msg.ReadInt32();

                if (NetworkManager.Instance.EntityList.TryGetValue(entityId, out var controllable))
                    partyMember.Controllable = controllable;
            }

            return partyMember;
        }
    }
}
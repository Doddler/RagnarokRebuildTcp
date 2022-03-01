using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using RebuildData.Shared.Enum;
using RebuildData.Shared.Networking;

namespace RebuildZoneServer.Networking.PacketHandlers
{
    class PacketSkill : ClientPacketHandler
    {
        public override PacketType PacketType => PacketType.Skill;

        public override void Process(InboundMessage msg)
        {
            if (connection.Character.State == CharacterState.Sitting ||
                connection.Character.State == CharacterState.Dead)
                return;

            connection.Player.PerformSkill();
        }
    }
}

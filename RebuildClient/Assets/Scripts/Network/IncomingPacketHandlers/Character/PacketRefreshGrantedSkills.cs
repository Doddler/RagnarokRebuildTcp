using System.Collections.Generic;
using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.RefreshGrantedSkills)]
    public class PacketRefreshGrantedSkills : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg) => State.SetGrantedSkills(ReadSkills(msg));

        public static List<(CharacterSkill skill, int level)> ReadSkills(ClientInboundMessage msg)
        {
            var count = msg.ReadInt16();
            var skills = new List<(CharacterSkill, int)>(count);
            for (var i = 0; i < count; i++)
                skills.Add(((CharacterSkill)msg.ReadInt16(), msg.ReadByte()));
            return skills;
        }
    }
}

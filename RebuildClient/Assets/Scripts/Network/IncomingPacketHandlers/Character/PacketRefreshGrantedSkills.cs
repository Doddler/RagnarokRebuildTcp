using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.RefreshGrantedSkills)]
    public class PacketRefreshGrantedSkills : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var grantedSkills = msg.ReadInt16();
            for (var i = 0; i < grantedSkills; i++)
                State.GrantedSkills.Add((CharacterSkill)msg.ReadInt16(), msg.ReadByte());

            UiManager.SkillManager.UpdateAvailableSkills();
        }
    }
}
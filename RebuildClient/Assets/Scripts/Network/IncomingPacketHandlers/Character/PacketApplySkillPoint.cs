using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ApplySkillPoint)]
    public class PacketApplySkillPoint : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var skill = (CharacterSkill)msg.ReadByte();
            var level = (int)msg.ReadByte();
            var newSkillPoints = msg.ReadInt32();

            State.SkillPoints = newSkillPoints;
            State.KnownSkills[skill] = level;
            UiManager.SkillManager.ApplySkillUpdateFromServer(skill, level);
            UiManager.SkillManager.RefreshSkillAvailability();
        }
    }
}
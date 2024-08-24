using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdatePlayerData)]
    public class PacketUpdatePlayerData : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var hp = msg.ReadInt32();
            var maxHp = msg.ReadInt32();
            var sp = msg.ReadInt32();
            var maxSp = msg.ReadInt32();
            State.SkillPoints = msg.ReadInt32();
            var skills = msg.ReadInt32();
            
            
            State.KnownSkills.Clear();
            for(var i = 0; i < skills; i++)
                State.KnownSkills.Add((CharacterSkill)msg.ReadByte(), msg.ReadByte());
            
            UiManager.SkillManager.UpdateAvailableSkills();
            CameraFollower.Instance.UpdatePlayerSP(sp, maxSp);
        }
    }
}
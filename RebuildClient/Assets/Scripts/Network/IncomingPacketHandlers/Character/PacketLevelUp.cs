using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.LevelUp)]
    public class PacketLevelUp : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var lvl = msg.ReadByte();
            var curExp = msg.ReadInt32();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            Camera.AttachEffectToEntity("LevelUp", controllable.gameObject, id);

            controllable.SetLevel(lvl);
            //the overflow carries into the new level
            if (controllable.IsMainCharacter)
                State.SetExp(curExp, State.JobExp);
        }
    }
}

using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.ChangeTarget)]
    public class PacketChangeTarget : ClientPacketHandlerBase
    {
        //id 0 ends the auto attack but keeps the selection
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (id == 0 || !Network.EntityList.TryGetValue(id, out var controllable))
                return;

            Camera.SetSelectedTarget(controllable);
        }
    }
}

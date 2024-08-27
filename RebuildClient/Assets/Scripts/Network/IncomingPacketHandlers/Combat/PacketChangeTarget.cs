using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.ChangeTarget)]
    public class PacketChangeTarget : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            if (id == 0)
            {
                Camera.ClearSelected();
                return;
            }

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            Camera.SetSelectedTarget(controllable, controllable.DisplayName, controllable.IsAlly, false);
        }
    }
}
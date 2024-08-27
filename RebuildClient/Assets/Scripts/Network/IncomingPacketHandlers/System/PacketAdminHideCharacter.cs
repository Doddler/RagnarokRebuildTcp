using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.System
{
    [ClientPacketHandler(PacketType.AdminHideCharacter)]
    public class PacketAdminHideCharacter : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var isHidden = msg.ReadBoolean();
            Debug.Log($"Packet for setting admin hide state: {isHidden}");

            State.IsAdminHidden = isHidden;

            if (Camera.TargetControllable)
            {
                Camera.TargetControllable.IsHidden = isHidden;
            }
        }
    }
}
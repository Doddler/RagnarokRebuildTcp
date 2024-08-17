using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.LookTowards)]
    public class PacketLookTowards : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var lookAt = msg.ReadPosition();
            var facing = (Direction)msg.ReadByte();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to move entity " + id + ", but it does not exist in scene!");
                return;
            }

            controllable.LookAt(lookAt.ToWorldPosition());
            
            if (controllable.SpriteAnimator.Type == SpriteType.Player)
                controllable.SpriteAnimator.SetHeadFacing((HeadFacing)msg.ReadByte());
        }
    }
}
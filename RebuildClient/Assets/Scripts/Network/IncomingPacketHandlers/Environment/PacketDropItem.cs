using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Network.PacketBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Environment
{
    [ClientPacketHandler(PacketType.DropItem)]
    public class PacketDropItem : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var groundId = msg.ReadInt32();
            var pos = new Vector2(msg.ReadFloat(), msg.ReadFloat());

            var id = msg.ReadInt32();
            var count = (int)msg.ReadInt16();
            var isAnimated = msg.ReadBoolean();
            var item = GroundItem.Create(groundId, id, count, pos, isAnimated);
            Network.GroundItemList.Add(groundId, item);
            
        }
    }
}
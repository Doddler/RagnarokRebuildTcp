using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.RemoveAllEntities)]
    public class PacketRemoveAllEntities : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            foreach (var entity in Network.EntityList)
            {
                GameObject.Destroy(entity.Value.gameObject);
            }

            Network.EntityList.Clear();
        }
    }
}
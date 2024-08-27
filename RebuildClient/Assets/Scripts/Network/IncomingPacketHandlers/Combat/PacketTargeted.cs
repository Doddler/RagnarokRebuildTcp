using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.Targeted)]
    public class PacketTargeted : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();

            //Debug.Log("TARGET! " + id);

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            var targetIcon = GameObject.Instantiate(Network.TargetNoticePrefab, controllable.transform);
            targetIcon.transform.localPosition = Vector3.zero;
        }
    }
}
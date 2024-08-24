using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdateMinimapMarker)]
    public class PacketUpdateMinimapMarker : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var count = msg.ReadInt16();
            for (var i = 0; i < count; i++)
            {
                var id = msg.ReadInt32();
                var pos = msg.ReadPosition();
                var type = (CharacterDisplayType)msg.ReadByte();

                if (id == NetworkManager.Instance.PlayerId)
                    continue;
                
                if(pos.x > 0 && pos.y > 0)
                    MinimapController.Instance.SetEntityPosition(id, type, pos);
                else
                    MinimapController.Instance.RemoveEntity(id);
            }
        }
    }
}
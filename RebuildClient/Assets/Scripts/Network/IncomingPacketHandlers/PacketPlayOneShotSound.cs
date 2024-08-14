using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Objects;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.PlayOneShotSound)]
    public class PacketPlayOneShotSound : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var fileName = msg.ReadString();
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);

            AudioManager.Instance.OneShotSoundEffect(-1, $"{fileName}.ogg", targetCell);
        }
    }
}
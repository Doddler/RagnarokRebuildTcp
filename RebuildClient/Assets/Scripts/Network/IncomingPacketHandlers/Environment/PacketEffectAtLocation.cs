using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Environment
{
    [ClientPacketHandler(PacketType.EffectAtLocation)]
    public class PacketEffectAtLocation : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var effect = msg.ReadInt32();
            var pos = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var facing = msg.ReadInt32();

            var spawn = Camera.WalkProvider.GetWorldPositionForTile(pos);

            if (!Camera.EffectList.TryGetValue(effect, out var asset))
                return;

            switch (asset.Name)
            {
                case "Explosion":
                    Camera.ShakeTime = 0.5f;
                    Camera.CreateEffect(effect, spawn, facing);
                    break;
                case "HammerFall":
                    HammerFallEffect.CreateHammerFall(spawn);
                    break;
                default:
                    Camera.CreateEffect(effect, spawn, facing);
                    break;
            }
            
            
        }
    }
}
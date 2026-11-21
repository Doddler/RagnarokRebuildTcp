using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Objects;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.CreateCastCircle)]
    public class PacketCreateCastCircle : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var size = (int)msg.ReadByte();
            var castTime = msg.ReadFloat();
            var isAlly = msg.ReadBoolean();
            var hasSound = msg.ReadBoolean();

            var targetCell = Camera.WalkProvider.GetWorldPositionForTile(target);
            CastTargetCircle.Create(isAlly, targetCell, size, castTime);
            if (hasSound)
                AudioManager.Instance.OneShotSoundEffect(-1, $"ef_beginspell.ogg", targetCell);
        }
    }
}

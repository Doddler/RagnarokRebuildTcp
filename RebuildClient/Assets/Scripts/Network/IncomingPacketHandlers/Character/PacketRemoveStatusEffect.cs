using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.RemoveStatusEffect)]
    public class PacketRemoveStatusEffect : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var status = (CharacterStatusEffect)msg.ReadByte();
            var isRefresh = msg.ReadBoolean();

            //no character while a map loads; the buff bar resets when the next one is bound
            if (Network.EntityList.TryGetValue(id, out var controllable))
                StatusEffectState.RemoveStatusFromTarget(controllable, status, isRefresh);
        }
    }
}
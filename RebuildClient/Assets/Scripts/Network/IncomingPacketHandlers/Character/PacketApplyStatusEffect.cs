using Assets.Scripts.Effects.EffectHandlers.StatusEffects;
using Assets.Scripts.Misc;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ApplyStatusEffect)]
    public class PacketApplyStatusEffect : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var status = (CharacterStatusEffect)msg.ReadByte();

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                // Debug.Log($"Applying status {status} to {controllable.Name}");
                StatusEffectState.AddStatusToTarget(controllable, status);
            }
        }
    }
}
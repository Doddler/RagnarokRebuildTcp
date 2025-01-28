using Assets.Scripts.Effects;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
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

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.Log($"Removing status {status} from {controllable.Name}");

                StatusEffectState.RemoveStatusFromTarget(controllable, status);
                StatusEffectPanel.Instance.RemoveStatusEffect(status);
            }
        }
    }
}
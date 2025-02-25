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
            var isRefresh = msg.ReadBoolean();

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.Log($"Removing status {status} from {controllable.Name}");

                StatusEffectState.RemoveStatusFromTarget(controllable, status);
                if(controllable.IsMainCharacter && !isRefresh)
                    StatusEffectPanel.Instance.RemoveStatusEffect(status);
            }
            else
            {
                Debug.Log($"Status effect {status} is removed while player not active on map.");
                if (NetworkManager.Instance.PlayerId == id && !isRefresh)
                    StatusEffectPanel.Instance.RemoveStatusEffect(status);
                    
            }
        }
    }
}
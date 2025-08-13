using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ApplyStatusEffect)]
    public class PacketApplyStatusEffect : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var status = (CharacterStatusEffect)msg.ReadByte();
            var duration = msg.ReadFloat();

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                // Debug.Log($"Applying status {status} to {controllable.Name}");
                StatusEffectState.AddStatusToTarget(controllable, status, false, duration);
                if (id == Network.PlayerId)
                    StatusEffectPanel.Instance.AddStatusEffect(status, duration);
            }
        }
    }
}
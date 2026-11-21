using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    //Applied like spawn statuses, not as newly gained.
    [ClientPacketHandler(PacketType.StatusEffectList)]
    public class PacketStatusEffectList : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var count = msg.ReadByte();
            Network.EntityList.TryGetValue(id, out var controllable);

            for (var i = 0; i < count; i++)
            {
                var status = (CharacterStatusEffect)msg.ReadByte();
                var duration = msg.ReadFloat();
                if (controllable == null)
                    continue;

                StatusEffectState.AddStatusToTarget(controllable, status, true, duration);
            }
        }
    }
}

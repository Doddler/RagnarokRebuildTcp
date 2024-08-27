using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.TakeDamage)]
    public class PacketTakeDamage : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id1 = msg.ReadInt32();

            if (!Network.EntityList.TryGetValue(id1, out var controllable))
            {
                Debug.LogWarning("Trying to have entity " + id1 + " take damage, but it does not exist in scene!");
                return;
            }

            var dmg = msg.ReadInt32();
            var hitCount = msg.ReadByte();
            var damageTiming = msg.ReadFloat();
            var lockTime = msg.ReadFloat();
            
            controllable.Messages.SendDamageEvent(null, damageTiming, dmg, hitCount);
        }
    }
}
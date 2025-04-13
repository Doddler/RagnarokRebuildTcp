using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.HpRecovery)]
    public class PacketHpRecovery : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var amnt = msg.ReadInt32();
            var hp = msg.ReadInt32();
            var maxHp = msg.ReadInt32();
            var type = (HealType)msg.ReadByte();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            controllable.Hp = hp;
            controllable.MaxHp = maxHp;

            if (controllable.IsMainCharacter)
                Camera.UpdatePlayerHP(hp, maxHp);
            controllable.SetHp(controllable.Hp, controllable.MaxHp);
            if (type == HealType.HealSkill)
            {
                HealEffect.CreateAutoLevel(controllable.gameObject, amnt);
                controllable.AttachHealIndicator(amnt);
            }
        }
    }
}
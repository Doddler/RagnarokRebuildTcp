using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdateCharacterDisplayState)]
    public class PacketUpdateCharacterDisplayState : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var position = (EquipPosition)msg.ReadByte();
            var itemId = msg.ReadInt32();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            if (position == EquipPosition.Weapon)
                controllable.WeaponClass = msg.ReadInt32();

            switch (position)
            {
                case EquipPosition.Weapon: ClientDataLoader.Instance.LoadAndAttachWeapon(controllable, itemId); break;
                case EquipPosition.HeadUpper: ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, itemId, EquipPosition.HeadUpper, 4); break;
                case EquipPosition.HeadMid: ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, itemId, EquipPosition.HeadMid, 3); break;
                case EquipPosition.HeadLower: ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, itemId, EquipPosition.HeadLower, 2); break;
            }
        }
    }
}
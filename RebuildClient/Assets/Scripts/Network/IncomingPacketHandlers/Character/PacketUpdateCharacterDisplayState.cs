using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
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
            
            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            var headUpper = msg.ReadInt32();
            var headMid = msg.ReadInt32();
            var headLower = msg.ReadInt32();
            var weapon = msg.ReadInt32();
            var shield = msg.ReadInt32();
            controllable.WeaponClass = msg.ReadInt32();
            
            var offHand = 0;
            if (shield > 0 && ClientDataLoader.Instance.TryGetItemById(shield, out var item) && item.ItemClass == ItemClass.Weapon)
            {
                offHand = item.SubType;
                shield = 0;
            }
            
            ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, headUpper, EquipPosition.HeadUpper, 6);
            ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, headMid, EquipPosition.HeadMid, 5);
            ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, headLower, EquipPosition.HeadLower, 4);
            ClientDataLoader.Instance.LoadAndAttachWeapon(controllable, weapon, offHand);
            ClientDataLoader.Instance.LoadAndAttachEquipmentSprite(controllable, shield, EquipPosition.Shield, 7);

            if (controllable.IsMainCharacter)
            {
                UiManager.Instance.EquipmentWindow.UpdateCharacterDisplay(headUpper, headMid, headLower);
                PlayerState.Instance.WeaponClass = controllable.WeaponClass;
            }

            Debug.Log($"Updating appearance data. New weapon class: {controllable.WeaponClass}");
        }
    }
}
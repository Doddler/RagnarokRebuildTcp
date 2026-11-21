using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.EquipUnequipGear)]
    public class PacketEquipUnequipGear : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var bagId = msg.ReadInt32();
            var slot = (EquipSlot)msg.ReadByte();
            var isEquip = msg.ReadBoolean();

            var item = State.Inventory.GetInventoryItem(bagId);
            if (item.ItemData.ItemClass == ItemClass.Ammo)
            {
                if (isEquip)
                    Camera.AppendChatText($"<color={TextColors.Equip}>Equipped {item.ProperName()}.</color>");
                else
                    Camera.AppendChatText($"<color={TextColors.Unequip}>Unequipped {item.ProperName()}.</color>");
            }
            else
            {
                if (isEquip)
                    Camera.AppendChatText($"<color={TextColors.Equip}>Put on {item.ProperName()}.</color>");
                else
                    Camera.AppendChatText($"<color={TextColors.Unequip}>Took off {item.ProperName()}.</color>");
            }
            
            if (slot == EquipSlot.Ammunition)
                State.SetAmmo(isEquip ? bagId : 0);
            else
                State.SetEquippedItem(slot, isEquip ? bagId : 0);
        }
    }
}
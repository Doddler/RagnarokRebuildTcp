using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

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
                if(isEquip)
                    Camera.AppendChatText($"<color=#00f6c7>Equipped {item.ProperName()}.</color>");
                else
                    Camera.AppendChatText($"<color=#c80000>Unequipped {item.ProperName()}.</color>");
            }
            else
            {
                if (isEquip)
                    Camera.AppendChatText($"<color=#00f6c7>Put on {item.ProperName()}.</color>");
                else
                    Camera.AppendChatText($"<color=#ed0000>Took off {item.ProperName()}.</color>");
            }
            
            if (slot == EquipSlot.Ammunition)
            {
                if (isEquip)
                    State.AmmoId = bagId;
                else
                    State.AmmoId = 0;
            }
            else
            {
                if (isEquip)
                    State.EquippedItems[(int)slot] = bagId;
                else
                    State.EquippedItems[(int)slot] = 0;
            }
            
            UiManager.EquipmentWindow.RefreshEquipmentWindow();
        }
    }
}
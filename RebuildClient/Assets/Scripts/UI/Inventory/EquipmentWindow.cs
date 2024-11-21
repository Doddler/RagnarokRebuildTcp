using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;

namespace Assets.Scripts.UI.Inventory
{
    public class EquipmentWindow : WindowBase
    {
        public EquipWindowEntry[] EquipEntries;
        public UiPlayerSprite PlayerSprite;
        
        public void RefreshEquipmentWindow()
        {
            var state = NetworkManager.Instance.PlayerState;
            var inventory = state.Inventory.GetInventoryData();
            
            for (var i = 0; i < 10; i++)
            {
                var bagId = state.EquippedItems[i];
                var pos = 1 << i;
                if (pos > (int)EquipPosition.Accessory)
                    pos = (int)EquipPosition.Accessory;

                if (bagId <= 0 || !inventory.TryGetValue(bagId, out var item) || (item.ItemData.Position & (EquipPosition)pos ) == 0)
                    EquipEntries[i].ClearSlot();
                else
                    EquipEntries[i].RefreshSlot(item);
            }

            for (var i = EquipSlot.HeadTop; i <= EquipSlot.HeadBottom; i++)
            {
                if (EquipEntries[(int)i].ItemId <= 0)
                    continue;

                var item = EquipEntries[(int)i].InventoryItem;
                var dat = item.ItemData;
                if(i != EquipSlot.HeadTop && (dat.Position & EquipPosition.HeadUpper) > 0) EquipEntries[(int)EquipSlot.HeadTop].RefreshSlot(item);
                if(i != EquipSlot.HeadMid && (dat.Position & EquipPosition.HeadMid) > 0) EquipEntries[(int)EquipSlot.HeadMid].RefreshSlot(item);
                if(i != EquipSlot.HeadBottom && (dat.Position & EquipPosition.HeadLower) > 0) EquipEntries[(int)EquipSlot.HeadBottom].RefreshSlot(item);
            }
            
            var weapon = EquipEntries[(int)EquipSlot.Weapon];
            if(weapon.ItemId > 0 && weapon.InventoryItem.ItemData.Position == EquipPosition.BothHands)
                EquipEntries[(int)EquipSlot.Shield].RefreshSlot(weapon.InventoryItem);
        }

        private void FixMultiSlotHeadgear(EquipSlot slot)
        {
            
        }

        public void UpdateCharacterDisplay(int headgear1, int headgear2, int headgear3)
        {
            var state = NetworkManager.Instance.PlayerState;
            
            PlayerSprite.PrepareDisplayPlayerCharacter(state.JobId, state.HairStyleId, state.HairColorId, headgear1, headgear2, headgear3, state.IsMale);
        }
    }
}
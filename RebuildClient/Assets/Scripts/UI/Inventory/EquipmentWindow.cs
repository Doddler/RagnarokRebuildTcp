using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Inventory
{
    public class EquipmentWindow : WindowBase
    {
        public EquipWindowEntry[] EquipEntries;
        public UiPlayerSprite PlayerSprite;
        public TextMeshProUGUI AmmoType;
        public TextMeshProUGUI CartInfo;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            var link = TMP_TextUtilities.FindIntersectingLink(AmmoType, eventData.position, null);
            if (link >= 0)
            {
                var ammo = PlayerState.Instance.AmmoId;
                if (ammo > 0)
                    NetworkManager.Instance.SendUnEquipItem(ammo);
            }
            
            link = TMP_TextUtilities.FindIntersectingLink(CartInfo, eventData.position, null);
            if (link >= 0)
            {
                NetworkManager.Instance.SendChangeCart(-1);
            }
        }


        public void RefreshEquipmentWindow()
        {
            var state = PlayerState.Instance;
            var inventory = state.Inventory.GetInventoryData();

            for (var i = 0; i < 10; i++)
            {
                var bagId = state.EquippedItems[i];
                var pos = 1 << i;
                if (pos > (int)EquipPosition.Accessory)
                    pos = (int)EquipPosition.Accessory;

                if (bagId <= 0 || !inventory.TryGetValue(bagId, out var item))
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
                if (i != EquipSlot.HeadTop && (dat.Position & EquipPosition.HeadUpper) > 0) EquipEntries[(int)EquipSlot.HeadTop].RefreshSlot(item);
                if (i != EquipSlot.HeadMid && (dat.Position & EquipPosition.HeadMid) > 0) EquipEntries[(int)EquipSlot.HeadMid].RefreshSlot(item);
                if (i != EquipSlot.HeadBottom && (dat.Position & EquipPosition.HeadLower) > 0) EquipEntries[(int)EquipSlot.HeadBottom].RefreshSlot(item);
            }

            var weapon = EquipEntries[(int)EquipSlot.Weapon];
            if (weapon.ItemId > 0 && weapon.InventoryItem.ItemData.Position == EquipPosition.BothHands)
                EquipEntries[(int)EquipSlot.Shield].RefreshSlot(weapon.InventoryItem);

            if (state.AmmoId > 0)
            {
                var ammo = ClientDataLoader.Instance.GetItemById(state.AmmoId);
                AmmoType.text = $"Ammo Type: {ammo.Name} (<color=#0000FF><link=\"RemoveAmmo\">Unequip</link></color>)";
            }
            else
                AmmoType.text = "";

            if (state.HasCart)
                CartInfo.text = "Push Cart (<color=#0000FF><link=\"RemoveCart\">Unequip</link></color>)";
            else
                CartInfo.text = "";
        }

        private void FixMultiSlotHeadgear(EquipSlot slot)
        {
        }

        public void UpdateCharacterDisplay(int headgear1, int headgear2, int headgear3)
        {
            var state = PlayerState.Instance;

            PlayerSprite.PrepareDisplayPlayerCharacter(state.JobId, state.HairStyleId, state.HairColorId, headgear1, headgear2, headgear3, state.IsMale);
        }
    }
}
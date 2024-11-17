using Assets.Scripts.Network;
using Assets.Scripts.Sprites;

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

                if (bagId <= 0 || !inventory.TryGetValue(bagId, out var item))
                    EquipEntries[i].ClearSlot();
                else
                    EquipEntries[i].RefreshSlot(item);
            }
        }

        public void UpdateCharacterDisplay(int headgear1, int headgear2, int headgear3)
        {
            var state = NetworkManager.Instance.PlayerState;
            
            PlayerSprite.PrepareDisplayPlayerCharacter(state.JobId, state.HairStyleId, state.HairColorId, headgear1, headgear2, headgear3, state.IsMale);
        }
    }
}
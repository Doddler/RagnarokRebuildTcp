using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.RefineItem
{
    public class RefineItemWindow : WindowBase
    {
        public Button OkButton;
        public ItemListEntryV2 MainTargetItem;
        public ItemListEntryV2 SelectedOre;
        public ItemListEntryV2 SelectedCatalyst;
        public TextMeshProUGUI CostText;
        public TextMeshProUGUI SuccessRateText;

        private static int[] costForType = {200, 1000, 5000, 20000, 2000};
        private static int[] overUpgradeRate = {80,60,30,20,20,20,10};
        private static int[] unsafePoint = { 7, 6, 5, 4, 4 };
        private static int[] itemForUpgrade = { 1010, 1011, 984, 984, 985 };
        private int itemRank = 0;
        private int activeSelectionType;
        
        private Dictionary<int, ItemListEntryV2> currentItemList = new();
        private GenericItemListV2 selectWindow;
        private ItemListEntryV2 selectedItem;
        private InventoryItem targetItem;
        private PlayerState state;

        public static RefineItemWindow Instance;

        public void CancelRefine()
        {
            NetworkManager.Instance.SendNpcAdvance();
            Destroy(gameObject);
        }

        public void SubmitRefine()
        {
            NetworkManager.Instance.SendNpcRefineAttempt(MainTargetItem.ItemId, SelectedOre.ItemId, SelectedCatalyst.ItemId);
            HideWindow();
            CameraFollower.Instance.DelayedExecuteAction(RevealAndRefresh, 1.4f);
        }

        public void RevealAndRefresh()
        {
            targetItem = state.Inventory.GetInventoryItem(MainTargetItem.ItemId);
            if(state.EquippedBagIdHashes.Contains(targetItem.BagSlotId))
                MainTargetItem.SetEquipped();
            else
                MainTargetItem.HideCount();
            itemRank = targetItem.ItemData.ItemRank;
            MainTargetItem.ItemName.text = targetItem.ProperName();
            
            ShowWindow();
            UpdateRefineValue();
        }

        public void SelectItem(int entryId)
        {
            Debug.Log($"Selected {entryId}");
            if (selectedItem != null)
            {
                if (selectedItem.UniqueEntryId == entryId)
                    return;
                selectedItem.Unselect();
            }

            selectedItem = currentItemList[entryId];
            selectWindow.OkButton.interactable = true;
        }

        public void OpenItemSelectWindow(int type)
        {
            if(selectWindow != null)
                Destroy(selectWindow);
            
            var prefab = UiManager.Instance.GenericItemListV2Prefab;
            var container = UiManager.Instance.PrimaryUserWindowContainer;
            var go = Instantiate(prefab, container);
            selectWindow = go.GetComponent<GenericItemListV2>();
            selectWindow.MoveToTop();
            selectWindow.CenterWindow();
            selectWindow.ToggleBox.gameObject.SetActive(false);
            selectWindow.InfoAreaText.gameObject.SetActive(false);
            go.AddComponent<HideWindowOnLostFocus>();
            activeSelectionType = type;
            selectedItem = null;
            currentItemList.Clear();
            
            switch (type)
            {
                case 0: StartSelectTargetItem(); break;
                    
            }
        }

        private void AddItemToSelectList(int id, InventoryItem item, PlayerState state)
        {
            var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(item.ItemData.Sprite);
            var entry = selectWindow.GetNewEntry();
            entry.Assign(DragItemType.None, sprite, item.BagSlotId, item.Count);
            entry.UniqueEntryId = id;
            entry.ItemName.text = item.ProperName();
            entry.ItemName.rectTransform.anchorMax = new Vector2(1, 1); //resize to full width
            if (state.EquippedBagIdHashes.Contains(item.BagSlotId))
                entry.SetEquipped();
            else
                entry.HideCount();

            entry.RightText.text = "";
            entry.CanDrag = false;
            entry.CanSelect = true;
            entry.EventOnSelect = SelectItem;
            entry.EventDoubleClick = FinishSelection;
            currentItemList.Add(id, entry);
        }
        
        private void StartSelectTargetItem()
        {
            var inventory = state.Inventory;
            var equipped = state.EquippedBagIdHashes;
            selectWindow.TitleBar.text = "Select Item to Refine";
            selectWindow.OkButtonText.text = "Select";
            selectWindow.OnPressCancel = () => Destroy(selectWindow.gameObject);
            selectWindow.OnPressOk = () => FinishSelection(selectedItem.UniqueEntryId);

            var id = 0;
            foreach (var (_, item) in inventory.GetInventoryData())
            {
                if (!item.ItemData.IsUnique || !item.ItemData.IsRefinable)
                    continue;
                AddItemToSelectList(id, item, state);
                id++;
            }
        }
        
        
        public void SelectOre(int id)
        {
            var item = ClientDataLoader.Instance.GetItemById(id);
            var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(item.Sprite);
            SelectedOre.Assign(DragItemType.None, sprite, id, 1);
            if (state.Inventory.GetItemCount(SelectedOre.ItemId) <= 0)
                SelectedOre.ItemName.text = $"<color=#B44E38>{item.Name} (Missing)";
            else
                SelectedOre.ItemName.text = item.Name;
        }
        
        public void StartSelectCatalyst(int id)
        {
            
        }

        public void FinishSelection(int selected)
        {
            SelectItem(selected);
            MainTargetItem.Assign(selectedItem);
            targetItem = state.Inventory.GetInventoryItem(selectedItem.ItemId);
            if(state.EquippedBagIdHashes.Contains(selectedItem.ItemId))
                MainTargetItem.SetEquipped();
            else
                MainTargetItem.HideCount();
            Destroy(selectWindow.gameObject);
            selectedItem = null;
            itemRank = targetItem.ItemData.ItemRank;
            MainTargetItem.ItemName.text = targetItem.ProperName();
            SelectOre(itemForUpgrade[itemRank]);
            
            UpdateRefineValue();
        }

        private void UpdateCatalystField()
        {
            
        }

        public void UpdateRefineValue()
        {
            if (MainTargetItem.ItemId <= 0)
            {
                CostText.text = "--";
                SuccessRateText.text = "--";
                SelectedOre.IsActive = false;
                SelectedCatalyst.IsActive = false;
                OkButton.interactable = false;
                return;
            }

            // if (SelectedOre.ItemId <= 0)
            // {
            //     CostText.text = "--z";
            //     SuccessRateText.text = $"--";
            //     SelectedOre.IsActive = true;
            //     SelectedCatalyst.IsActive = false;
            //     return;
            // }

            var cost = costForType[itemRank];

            if (SelectedOre.ItemId <= 0 || state.Inventory.GetItemCount(SelectedOre.ItemId) <= 0)
            {
                CostText.text = state.Zeny >= cost ? $"{cost:N0}z" : $"<color=#B44E38>{cost:N0}z (Insufficient zeny)";
                SuccessRateText.text = $"<color=#B44E38>0%\n<size=-4>You do not have the required ore to upgrade.";
                SelectedOre.IsActive = false;
                SelectedCatalyst.IsActive = false;
                OkButton.interactable = false;
                return;
            }

            OkButton.interactable = state.Zeny >= cost;
            CostText.text = state.Zeny >= cost ? $"{cost:N0}z" : $"<color=#B44E38>{cost:N0}z (Insufficient zeny)";
            var curUpgrade = (int)targetItem.UniqueItem.Refine;
            var safeLevel = unsafePoint[itemRank];
            if (curUpgrade < safeLevel)
                SuccessRateText.text = "100%\n<size=-4>Refining this item is guaranteed to succeed.";
            else
            {
                var unsafeRank = curUpgrade - safeLevel;
                if (unsafeRank >= overUpgradeRate.Length)
                    unsafeRank = overUpgradeRate.Length - 1;
                var chance = overUpgradeRate[unsafeRank];
                SuccessRateText.text = $"<color=#B44E38>{chance}%\n<size=-4>This item will lose one upgrade on failure.";
            }
        }
        
        public void Init()
        {
            state = NetworkManager.Instance.PlayerState;
            OkButton.interactable = false;
            MainTargetItem.EventOnClick = OpenItemSelectWindow;
            SelectedOre.IsActive = false;
            SelectedCatalyst.IsActive = false;
            UpdateRefineValue();
        }
        
        public static void OpenRefineItemWindow()
        {
            var go = Instantiate(UiManager.Instance.RefineWindowPrefab, UiManager.Instance.PrimaryUserWindowContainer.transform);
            var window = go.GetComponent<RefineItemWindow>();
            Instance = window;
            window.Init();
            window.MoveToTop();
            window.CenterWindow();
        }
    }
}
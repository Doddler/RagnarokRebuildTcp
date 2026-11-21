using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.UI.Utility;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI
{
    public enum StorageSectionType
    {
        Uninitialized = -1,
        All,
        Useable,
        Weapon,
        Armor,
        Ammo,
        Card,
        Etc
    }
    
    public class StorageUI : MonoBehaviour
    {
        public GenericItemListV2 ItemWindow;
        public TabGroupVisual StorageTabGroup;
        public static StorageUI Instance;
        
        public StorageSectionType CurrentSection;
        private Dictionary<int, ItemListEntryV2> itemList;
        private ClientInventory itemBag;

        public void ChangeStorageTab(StorageSectionType newType)
        {
            if (newType == CurrentSection)
                return;
            CurrentSection = newType;
            RefreshItemList();
        }

        public void ClickStorageTab(int type)
        {
            ChangeStorageTab((StorageSectionType)type);
        }

        public void UpdateDropArea(bool isActive)
        {
            ItemWindow.DropZone.DisableDropArea();
            ItemWindow.DropZone.gameObject.SetActive(isActive);
        }
        
        private void CloseStorage()
        {
            NetworkManager.Instance.SendEndStorage();
            Destroy(gameObject);
        }

        //patches only the changed entry
        private void OnStorageChanged(int bagId)
        {
            if (bagId == ClientInventory.AllItems)
            {
                RefreshItemList();
                return;
            }

            RefreshItemCount();

            if (!itemBag.TryGetInventoryItem(bagId, out var item))
            {
                if (itemList.Remove(bagId, out var removed))
                    ItemWindow.ReturnItemListEntry(removed);
                return;
            }

            if (!IsInSection(item, CurrentSection))
                return;

            if (itemList.TryGetValue(bagId, out var entry))
                entry.UpdateCount(item.Count);
            else
                AddItem(item);
        }

        private static StorageSectionType SectionFor(ItemClass itemClass) => itemClass switch
        {
            ItemClass.Useable => StorageSectionType.Useable,
            ItemClass.Weapon => StorageSectionType.Weapon,
            ItemClass.Equipment => StorageSectionType.Armor,
            ItemClass.Ammo => StorageSectionType.Ammo,
            ItemClass.Card => StorageSectionType.Card,
            _ => StorageSectionType.Etc
        };

        private static bool IsInSection(InventoryItem item, StorageSectionType section) =>
            section == StorageSectionType.All || SectionFor(item.ItemData.ItemClass) == section;

        //an item put into storage brings up its tab, so it shows when it arrives
        private void ShowSectionFor(InventoryItem item)
        {
            if (!IsInSection(item, CurrentSection))
                SelectStorageTab(SectionFor(item.ItemData.ItemClass));
        }
        
        public void SetupStorageItems(ClientInventory bag)
        {
            ItemWindow.HasSubmitButtons = false;
            ItemWindow.HasToggleButton = false;
            // ItemWindow.ShowItemValues = false;
            ItemWindow.OkButton.interactable = false;
            ItemWindow.OkButton.gameObject.SetActive(false);
            ItemWindow.CancelButton.gameObject.SetActive(true);
            ItemWindow.ToggleBox.gameObject.SetActive(false);
            ItemWindow.InfoAreaText.gameObject.SetActive(true);
            ItemWindow.TitleBar.text = $"Storage";
            ItemWindow.CancelButtonText.text = $"Close";
            ItemWindow.OnPressCancel = CloseStorage;
            ItemWindow.OnCloseWindow = CloseStorage;
            ItemWindow.OnPressOk = null;
            ItemWindow.MoveToTop();

            ItemWindow.DropZone.ValidDragOrigins = ItemDragOrigin.ItemWindow;
            ItemWindow.DropZone.OnDropItem = OnDropInventoryItem;
            ItemWindow.SetActive();
            var pos = GameConfig.Data.StoragePosition;
            if (pos == Vector2.zero)
                ItemWindow.CenterWindow();
            else
            {
                ItemWindow.RectTransform().anchoredPosition = pos;
                ItemWindow.FitWindowIntoPlayArea();
            }

            itemBag = bag;
            itemBag.Changed += OnStorageChanged;
            itemList = new Dictionary<int, ItemListEntryV2>();
            
            var targetSection = CurrentSection;
            Debug.Log($"Setup storage with initial tab: {CurrentSection}");
            CurrentSection = StorageSectionType.Uninitialized; //it won't change if it thinks we're already on the tab... so lets change it
            SelectStorageTab(targetSection);
        }

        private void SelectStorageTab(StorageSectionType section)
        {
            StorageTabGroup.SelectTab((int)section, true);
        }

        public void OnDropInventoryItem(ItemDragObject srcItem)
        {
            // var srcItem = UiManager.Instance.DragItemObject;
            Debug.Log($"Dropped item {srcItem.ItemId}");

            if (PlayerState.Instance.Inventory.GetInventoryData().TryGetValue(srcItem.ItemId, out var item))
            {
                ShowSectionFor(item);
                if (item.ItemData.IsUnique || item.Count == 1)
                    NetworkManager.Instance.SendMoveStorageItem(srcItem.ItemId, srcItem.ItemCount, true);
                else
                    UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.InventoryToStorage);
            }
        }
        
        public void OnMoveInventoryItemToStorage(int bagSlotId)
        {
            Debug.Log($"Moving item to storage: {bagSlotId}");

            if (PlayerState.Instance.Inventory.GetInventoryData().TryGetValue(bagSlotId, out var item))
            {
                ShowSectionFor(item);
                if (item.ItemData.IsUnique || item.Count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    NetworkManager.Instance.SendMoveStorageItem(bagSlotId, item.Count, true);
                else
                    UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.InventoryToStorage);
            }
        }

        public void OnMoveItemToInventory(int bagId)
        {
            if (bagId >= ClientDataLoader.UniqueItemStartId)
                NetworkManager.Instance.SendMoveStorageItem(bagId, 1, false);
            else
            {
                if (PlayerState.Instance.Storage.GetInventoryData().TryGetValue(bagId, out var item))
                {
                    if (item.ItemData.IsUnique || item.Count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        NetworkManager.Instance.SendMoveStorageItem(bagId, item.Count, false);
                    else
                        UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.StorageToInventory);
                }
            }
        }
        
        private void OnRightClick(ItemListEntryV2 entry)
        {
            if (PlayerState.Instance.HasCart && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            {
                OnMoveItemToInventory(entry.UniqueEntryId);
                return;
            }

            UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(entry.ItemId);
        }

        private void RefreshItemCount()
        {
            ItemWindow.InfoAreaText.text = $"Total Items: {itemBag.TotalItems} / 600";
        }

        public void RefreshItemList()
        {
            foreach(var (key, item) in itemList)
                ItemWindow.ReturnItemListEntry(item);
            
            RefreshItemCount();
            
            itemList.Clear();
            
            foreach (var (key, item) in itemBag.GetInventoryData())
                if (IsInSection(item, CurrentSection))
                    AddItem(item);
        }

        private void AddItem(InventoryItem item)
        {
            var entry = ItemWindow.GetNewEntry();
            var sprite = ClientDataLoader.Instance.GetIconAtlasSprite(item.ItemData.Sprite);
            if(item.Type == ItemType.RegularItem)
                entry.Assign(DragItemType.StorageItem, sprite, item.Id, item.Count);
            else
                entry.Assign(DragItemType.StorageItem, sprite, item.Id, 1);
            
            entry.ItemName.text = item.ProperName();
            entry.UniqueEntryId = item.BagSlotId;
            entry.RightText.text = "";
            entry.CanDrag = true;
            entry.DragOrigin = ItemDragOrigin.StorageWindow;
            entry.EventDoubleClick = OnDoubleClickItem;
            entry.EventOnRightClick = (itemId) =>
            {
                if(itemList.TryGetValue(itemId, out var item))
                    OnRightClick(item);
            };
            
            // entry.ImageDisplayGroup.transform.localPosition += new Vector3(5, 0, 0);
            // entry.ItemName.transform.localPosition += new Vector3(10, 0, 0);
            itemList.Add(item.BagSlotId, entry);
        }

        private void OnDoubleClickItem(int id)
        {
            if (!itemList.TryGetValue(id, out var entry))
                return;
            
            OnMoveItemToInventory(id);
        }
        
        public static StorageUI InitializeStorageUI(GameObject storageListPrefab, RectTransform parentContainer)
        {
            var window = Instantiate(storageListPrefab, parentContainer);
            var shop = window.AddComponent<StorageUI>();
            Instance = shop;

            shop.ItemWindow = window.GetComponent<GenericItemListV2>();
            shop.StorageTabGroup = window.GetComponentInChildren<TabGroupVisual>(true);
            shop.StorageTabGroup.OnTabSelected.RemoveListener(shop.ClickStorageTab);
            shop.StorageTabGroup.OnTabSelected.AddListener(shop.ClickStorageTab);
            shop.CurrentSection = (StorageSectionType)GameConfig.Data.LastStorageTab;
            
            return shop;
        }

        public void OnDestroy()
        {
            Instance = null;
            if (itemBag != null)
                itemBag.Changed -= OnStorageChanged;
            GameConfig.Data.LastStorageTab = (int)CurrentSection;
            GameConfig.Data.StoragePosition = ItemWindow.RectTransform().anchoredPosition;
            ItemWindow.CloseWindow();
        }
    }
}

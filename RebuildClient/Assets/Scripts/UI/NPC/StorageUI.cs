using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
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
        public StorageControls StorageControls;
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

        public void UpdateStorageItemCount(InventoryItem item, int change)
        {
            if (change > 0)
            {
                if (!itemList.TryGetValue(item.BagSlotId, out var entry))
                {
                    AddItem(item);
                }
                else
                {
                    entry.UpdateCount(item.Count);
                }

                itemBag.UpdateItem(item);
            }
            else
            {
                if (!itemList.TryGetValue(item.BagSlotId, out var entry))
                    return;
                
                itemBag.RemoveItem(item.BagSlotId, -change);

                if (entry.ItemCount + change <= 0) //change will be negative here
                {
                    itemList.Remove(item.BagSlotId);
                    ItemWindow.ReturnItemListEntry(entry);
                }
                else
                    entry.UpdateCount(entry.ItemCount + change);
            }
            
            RefreshItemCount();

            if (CurrentSection == StorageSectionType.All)
                return;
            
            if(CurrentSection != StorageSectionType.Useable && item.ItemData.ItemClass == ItemClass.Useable) 
                StorageControls.ClickStorageButton((int)StorageSectionType.Useable);
            if(CurrentSection != StorageSectionType.Weapon && item.ItemData.ItemClass == ItemClass.Weapon) 
                StorageControls.ClickStorageButton((int)StorageSectionType.Weapon);
            if(CurrentSection != StorageSectionType.Armor && item.ItemData.ItemClass == ItemClass.Equipment) 
                StorageControls.ClickStorageButton((int)StorageSectionType.Armor);
            if(CurrentSection != StorageSectionType.Ammo && item.ItemData.ItemClass == ItemClass.Ammo) 
                StorageControls.ClickStorageButton((int)StorageSectionType.Ammo);
            if(CurrentSection != StorageSectionType.Card && item.ItemData.ItemClass == ItemClass.Card) 
                StorageControls.ClickStorageButton((int)StorageSectionType.Card);
            if(CurrentSection != StorageSectionType.Etc && item.ItemData.ItemClass == ItemClass.Etc) 
                StorageControls.ClickStorageButton((int)StorageSectionType.Etc);
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
            itemList = new Dictionary<int, ItemListEntryV2>();
            
            var targetSection = CurrentSection;
            Debug.Log($"Setup storage with initial tab: {CurrentSection}");
            CurrentSection = StorageSectionType.Uninitialized; //it won't change if it thinks we're already on the tab... so lets change it
            StorageControls.ClickStorageButton((int)targetSection);
        }

        public void OnDropInventoryItem(ItemDragObject srcItem)
        {
            // var srcItem = UiManager.Instance.DragItemObject;
            Debug.Log($"Dropped item {srcItem.ItemId}");

            if (NetworkManager.Instance.PlayerState.Inventory.GetInventoryData().TryGetValue(srcItem.ItemId, out var item))
            {
                if (item.ItemData.IsUnique || item.Count == 1)
                    NetworkManager.Instance.SendMoveStorageItem(srcItem.ItemId, srcItem.ItemCount, true);
                else
                    UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.InventoryToStorage);
            }
        }
        
        public void OnMoveInventoryItemToStorage(int bagSlotId)
        {
            Debug.Log($"Moving item to storage: {bagSlotId}");

            if (NetworkManager.Instance.PlayerState.Inventory.GetInventoryData().TryGetValue(bagSlotId, out var item))
            {
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
                if (NetworkManager.Instance.PlayerState.Storage.GetInventoryData().TryGetValue(bagId, out var item))
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
            {
                switch (CurrentSection)
                {
                    case StorageSectionType.All:
                        break;
                    case StorageSectionType.Useable:
                        if (item.ItemData.ItemClass != ItemClass.Useable) continue;
                        break;
                    case StorageSectionType.Weapon:
                        if (item.ItemData.ItemClass != ItemClass.Weapon) continue;
                        break;
                    case StorageSectionType.Armor:
                        if (item.ItemData.ItemClass != ItemClass.Equipment) continue;
                        break;
                    case StorageSectionType.Ammo:
                        if (item.ItemData.ItemClass != ItemClass.Ammo) continue;
                        break;
                    case StorageSectionType.Card:
                        if (item.ItemData.ItemClass != ItemClass.Card) continue;
                        break;
                    default: //etc
                        if(item.ItemData.ItemClass is ItemClass.Useable or ItemClass.Equipment or ItemClass.Weapon or ItemClass.Ammo or ItemClass.Card)
                            continue;
                        break;
                }
                
                AddItem(item);
            }
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
            shop.StorageControls = window.GetComponent<StorageControls>();
            shop.CurrentSection = (StorageSectionType)GameConfig.Data.LastStorageTab;
            
            return shop;
        }

        public void OnDestroy()
        {
            Instance = null;
            GameConfig.Data.LastStorageTab = (int)CurrentSection;
            GameConfig.Data.StoragePosition = ItemWindow.RectTransform().anchoredPosition;
            ItemWindow.CloseWindow();
        }
    }
}
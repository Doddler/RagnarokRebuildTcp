using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Toggle = UnityEngine.UI.Toggle;

namespace Assets.Scripts.UI
{
    public enum ItemListRole
    {
        BuyFromNpcItemList,
        BuyFromNpcSummary,
        SellToNpcItemList,
        SellToNpcSummary,
        ItemStorage,
        CardSocketing
    }

    public class ItemListWindow : WindowBase
    {
        public ItemListInteractionDropZone DropZone;
        public GameObject ItemListEntryPrefab;
        public Transform ItemListParentBox;
        public ScrollRect ScrollRect;
        public Button OkButton;
        public Button CancelButton;
        public Toggle ToggleBox;
        public TextMeshProUGUI TitleBar;
        public TextMeshProUGUI InfoAreaText;
        public TextMeshProUGUI ToggleText;
        public TextMeshProUGUI OkButtonText;
        public TextMeshProUGUI CancelButtonText;
        
        public Action OnPressOk;
        public Action OnPressCancel;
        public Action<bool> OnPressCheckbox;

        [NonSerialized] public List<ItemListEntry> ItemListEntries;
        [NonSerialized] public Stack<ItemListEntry> UnusedEntries;

        [NonSerialized] public ItemListRole CurrentRole;

        public int ItemAdjustValue;

        public override void HideWindow()
        {
            ShopUI.Instance.OnCancel();
        }

        public void Prepare(ItemListRole role)
        {
            CurrentRole = role;
            ItemListEntryPrefab.SetActive(false);

            ItemListEntries = new List<ItemListEntry>();
            UnusedEntries = new Stack<ItemListEntry>();
            
            foreach (var i in ItemListEntries)
            {
                i.gameObject.SetActive(false);
                UnusedEntries.Push(i);
            }
            
            switch (role)
            {
                case ItemListRole.BuyFromNpcSummary:
                    ToggleBox.gameObject.SetActive(false);
                    InfoAreaText.gameObject.SetActive(true);
                    OkButton.gameObject.SetActive(true);
                    CancelButton.gameObject.SetActive(true);
                    TitleBar.text = "Buying Items";
                    OkButtonText.text = "Buy";
                    CancelButtonText.text = "Cancel";
                    InfoAreaText.text = "Total: 0 Zeny";
                    break;
                case ItemListRole.SellToNpcSummary:
                    ToggleBox.gameObject.SetActive(false);
                    InfoAreaText.gameObject.SetActive(true);
                    OkButton.gameObject.SetActive(true);
                    CancelButton.gameObject.SetActive(true);
                    TitleBar.text = "Selling Items";
                    OkButtonText.text = "Sell";
                    CancelButtonText.text = "Cancel";
                    InfoAreaText.text = "Total: 0 Zeny";
                    break;
                case ItemListRole.BuyFromNpcItemList:
                    ToggleBox.gameObject.SetActive(false);
                    InfoAreaText.gameObject.SetActive(false);
                    OkButton.gameObject.SetActive(false);
                    CancelButton.gameObject.SetActive(false);
                    TitleBar.text = "Shop Items";
                    break;
                case ItemListRole.SellToNpcItemList:
                    ToggleBox.gameObject.SetActive(true);
                    InfoAreaText.gameObject.SetActive(false);
                    OkButton.gameObject.SetActive(false);
                    CancelButton.gameObject.SetActive(false);
                    TitleBar.text = "Items Available to Sell";
                    ToggleText.text = "Prompt for item amount";
                    break;
            }
        }

        public void OnSubmit()
        {
            OnPressOk();
        }

        public void OnCancel()
        {
            OnPressCancel();
        }

        public void RemoveItemEntry(ShopEntry removeItem)
        {
            for (var i = 0; i < ItemListEntries.Count; i++)
            {
                if (ItemListEntries[i].ItemId != removeItem.ItemId)
                    continue;

                UnusedEntries.Push(ItemListEntries[i]);
                ItemListEntries[i].gameObject.SetActive(false);
                ItemListEntries[i].ItemId = -1;
                ItemListEntries[i].ItemCount = 0;
                ItemListEntries.RemoveAt(i);
                break;
            }
        }

        public void UpdateExistingCount(int itemId, int newCount)
        {
            foreach (var current in ItemListEntries)
            {
                if (current.ItemId == itemId)
                {
                    var cost = GetCost(itemId, newCount);
                    var adjustCost = ItemAdjustValue > 0 ? GetAdjustCost(itemId, newCount) : cost;
                    current.Assign(current.Sprite, itemId, newCount);
                    if(cost == adjustCost)
                        current.RightText.text = $"{GetCost(itemId, newCount):N0}z";
                    else
                        current.RightText.text = $"<s><color=#b0b0b0>{cost:N0}z</color></s>\n{adjustCost:N0}z";
                    return;
                }
            }
        }

        private int GetCost(int itemId, int count)
        {
            switch (CurrentRole)
            {
                case ItemListRole.BuyFromNpcSummary:
                case ItemListRole.BuyFromNpcItemList:
                {
                    var item = ClientDataLoader.Instance.GetItemById(itemId);
                    return item.Price * count;
                }
                case ItemListRole.SellToNpcItemList:
                {
                    var item = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(itemId);
                    return item.SalePrice;
                }
                case ItemListRole.SellToNpcSummary:
                {
                    var item = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(itemId);
                    return item.SalePrice * count;
                }
            }

            return 0;
        }
        
        private int GetAdjustCost(int itemId, int count)
        {
            switch (CurrentRole)
            {
                case ItemListRole.BuyFromNpcSummary:
                case ItemListRole.BuyFromNpcItemList:
                {
                    var item = ClientDataLoader.Instance.GetItemById(itemId);
                    return (item.Price - item.Price * ItemAdjustValue / 100) * count;
                }
                case ItemListRole.SellToNpcItemList:
                {
                    var item = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(itemId);
                    return item.SalePrice * (100 + ItemAdjustValue) / 100;
                }
                case ItemListRole.SellToNpcSummary:
                {
                    var item = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(itemId);
                    return item.SalePrice * (100 + ItemAdjustValue) / 100 * count;
                }
            }

            return 0;
        }

        public void AddItemEntry(ShopEntry addItem, bool canStack)
        {
            ItemListEntry entry = null;
            if (canStack)
            {
                foreach (var current in ItemListEntries)
                {
                    if (current.ItemId == addItem.ItemId)
                    {
                        entry = current;
                        addItem.Count += current.ItemCount;
                        break;
                    }
                }
            }

            var hasExisting = entry != null;
            if (entry == null && !UnusedEntries.TryPop(out entry))
            {
                var go = GameObject.Instantiate(ItemListEntryPrefab, ItemListParentBox);
                entry = go.GetComponent<ItemListEntry>();
            }

            var spriteName = ClientDataLoader.Instance.GetItemById(addItem.ItemId).Sprite;
            var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(spriteName);

            var adjustValue = addItem.Cost - addItem.Cost * ItemAdjustValue / 100;
            
            entry.gameObject.SetActive(true);
            if (addItem.Count <= 0)
            {
                entry.Assign(sprite, addItem.ItemId, 0);
                if(ItemAdjustValue > 0 && adjustValue != addItem.Cost)
                    entry.RightText.text = $"<s><color=#b0b0b0>{addItem.Cost:N0}z</color></s>\n{adjustValue:N0}z";
                else
                    entry.RightText.text = $"{addItem.Cost:N0}z";
            }
            else
            {
                var cost = addItem.Cost * addItem.Count;
                var adjustCost = adjustValue * addItem.Count;
                entry.Assign(sprite, addItem.ItemId, addItem.Count);
                if(ItemAdjustValue > 0 && cost != adjustCost)
                    entry.RightText.text = $"<s><color=#b0b0b0>{cost:N0}z</color></s>\n{adjustCost:N0}z";
                else
                    entry.RightText.text = $"{cost:N0}z";
            }
            
            entry.OnPointerExit(null);
            entry.Role = CurrentRole;
            if (!hasExisting)
            {
                entry.transform.SetAsLastSibling();
                if (CurrentRole == ItemListRole.BuyFromNpcSummary)
                {
                    Canvas.ForceUpdateCanvases();
                    ScrollRect.verticalNormalizedPosition = 0;
                }
            }

            ItemListEntries.Add(entry);
        }

        public void AddItemEntry(InventoryItem addItem, ShopEntry shopEntry)
        {
            //unlike the other overload, this one is only used initially because it pulls directly from the inventory
            var spriteName = addItem.ItemData.Sprite;
            var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(spriteName);
            
            ItemListEntry listEntry = null;
            
            if (!addItem.ItemData.IsUnique)
            {
                foreach (var current in ItemListEntries)
                {
                    if (current.ItemId == shopEntry.ItemId)
                    {
                        listEntry = current;
                        shopEntry.Count += current.ItemCount;
                        break;
                    }
                }
            }
            
            if (listEntry == null && !UnusedEntries.TryPop(out listEntry))
            {
                var go = GameObject.Instantiate(ItemListEntryPrefab, ItemListParentBox);
                listEntry = go.GetComponent<ItemListEntry>();
            }

            var adjustValue = 0;

            if (CurrentRole == ItemListRole.SellToNpcItemList)
            {
                shopEntry.Cost = addItem.SalePrice;
                adjustValue = addItem.SalePrice * (100 + ItemAdjustValue) / 100;
            }
            else
            {
                shopEntry.Cost = shopEntry.Count * addItem.SalePrice;
                adjustValue = addItem.SalePrice * (100 + ItemAdjustValue) / 100 * shopEntry.Count;
            }

            listEntry.gameObject.SetActive(true);
            listEntry.Assign(sprite, addItem.ItemData, shopEntry.ItemId, shopEntry.Count);
            listEntry.ItemName.text = addItem.ProperName();
            if(adjustValue > 0 && adjustValue == shopEntry.Cost)
                listEntry.RightText.text = $"{shopEntry.Cost:N0}z";
            else
                listEntry.RightText.text = $"<s><color=#b0b0b0>{shopEntry.Cost:N0}z</color></s>\n{adjustValue:N0}z";
            listEntry.OnPointerExit(null);

            listEntry.Role = CurrentRole;
            listEntry.transform.SetAsLastSibling();
            ItemListEntries.Add(listEntry);
            if (CurrentRole == ItemListRole.SellToNpcSummary)
            {
                Canvas.ForceUpdateCanvases();
                ScrollRect.verticalNormalizedPosition = 0;
            }
        }

        public void ShowText(string text)
        {
            
        }

        public void HideInfoPanel()
        {
            
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI
{
    public struct ShopEntry
    {
        public int ItemId;
        public int Cost;
        public int Count;
    }

    public struct ShopDragData
    {
        public ItemListRole DragSource;
        public ItemDragOrigin DragOrigin;
        public int ItemId;
        public int Count;
    }

    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance;

        public ItemListWindow LeftWindow;
        public ItemListWindow RightWindow;

        //left window items (itemId / shopEntry)
        private Dictionary<int, ShopEntry> LeftSideItems = new();
        //this isn't a dictionary because if you buy from an NPC the keys wouldn't be unique.
        //This is probably the biggest mistake in all of this.
        private List<ShopEntry> RightSideItems = new();

        private bool isBuyingFromNpc;


        public void OnFinish()
        {
            //we submit the same way regardless of if it's a purchase or sale, the server knows what we're doing.
            NetworkManager.Instance.SubmitShopPurchase(RightSideItems);
            EndShopUI();
        }

        public void OnCancel()
        {
            NetworkManager.Instance.SubmitShopPurchase(null);
            EndShopUI();
        }

        private void EndShopUI()
        {
            var mgr = UiManager.Instance;
            mgr.ForceHideTooltip();
            if (mgr.WindowStack.Contains(LeftWindow))
                mgr.WindowStack.Remove(LeftWindow);
            if (mgr.WindowStack.Contains(RightWindow))
                mgr.WindowStack.Remove(RightWindow);

            Destroy(RightWindow.gameObject); //right first, we're on the left!
            Destroy(LeftWindow.gameObject);
            Instance = null;
            UiManager.Instance.ItemDescriptionWindow.HideWindow(); //probably fine
        }

        public void OnStartDrag(ItemListRole role)
        {
            switch (role)
            {
                case ItemListRole.BuyFromNpcItemList:
                case ItemListRole.SellToNpcItemList:
                    RightWindow.DropZone.gameObject.SetActive(true);
                    break;
                case ItemListRole.BuyFromNpcSummary:
                case ItemListRole.SellToNpcSummary:
                    LeftWindow.DropZone.gameObject.SetActive(true);
                    break;
            }
        }

        public void OnEndDrag()
        {
            LeftWindow.DropZone.gameObject.SetActive(false);
            LeftWindow.DropZone.DisableDropArea();
            RightWindow.DropZone.gameObject.SetActive(false);
            RightWindow.DropZone.DisableDropArea();
        }

        private void OnDropItemBuyFromNpc(ShopDragData dragData)
        {
            var data = ClientDataLoader.Instance.GetItemById(dragData.ItemId);

            if (dragData.DragSource == ItemListRole.BuyFromNpcSummary && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                if(RemoveRightSideItem(dragData.ItemId, out var existing))
                    RightWindow.RemoveItemEntry(existing);
                RefreshRightSideCost();
                return;
            }

            //don't allow them to add any more items after 12 if they don't stack with anything
            if (dragData.DragSource == ItemListRole.BuyFromNpcItemList && RightSideItems.Count > 12)
            {
                if(data.IsUnique)
                    return;
                if (RightSideItems.All(r => r.ItemId != data.Id))
                    return;
            }

            if (data.IsUnique || dragData.Count == 1)
                FinalizeDropWithCount(dragData, 1);
            else
            {
                dragData.Count = GetRightSideCountForConfirmation(dragData.ItemId);
                UiManager.Instance.DropCountConfirmationWindow.BeginShopItemDrop(dragData, data.Name);
            }
        }

        public void OnDropItemSellToNpc(ShopDragData dragData)
        {
            var inventoryData = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(dragData.ItemId);
            var moveFullStack = !LeftWindow.ToggleBox.isOn;

            if (dragData.DragSource == ItemListRole.SellToNpcItemList)
            {
                var existingLeftSide = LeftSideItems[dragData.ItemId];
                
                if (moveFullStack || dragData.Count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    FinalizeDropWithCount(dragData, dragData.Count);
                else
                {
                        dragData.Count = existingLeftSide.Count;
                        UiManager.Instance.DropCountConfirmationWindow.BeginShopItemDrop(dragData, inventoryData.ProperName());   
                }

                RefreshRightSideCost();
            }

            if (dragData.DragSource == ItemListRole.SellToNpcSummary)
            {
                if (moveFullStack || dragData.Count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    FinalizeDropWithCount(dragData, dragData.Count);
                else
                    UiManager.Instance.DropCountConfirmationWindow.BeginShopItemDrop(dragData, inventoryData.ProperName());

                RefreshRightSideCost();
            }
        }
        
        public void OnDropItem(ShopDragData dragData)
        {
            OnEndDrag();
            if (isBuyingFromNpc)
                OnDropItemBuyFromNpc(dragData);
            else
                OnDropItemSellToNpc(dragData);
        }

        private int GetRightSideCountForConfirmation(int itemId)
        {
            foreach(var item in RightSideItems)
                if (item.ItemId == itemId)
                    return item.Count;
            return 1;
        }

        //if you drag from the right side into the background, we remove it from the list
        public void OnDropTrash(ItemDragObject obj)
        {
            OnEndDrag();
            var dropRole = (ItemListRole)obj.OriginId;
            if (dropRole != ItemListRole.BuyFromNpcSummary)
                return;
            
            OnDropItem(new ShopDragData() {DragSource = dropRole, Count = obj.ItemCount, ItemId = obj.ItemId});
        }

        //Removes first occurence. The only way an item can be on here twice is that both are unique, so counts don't matter. 
        private bool RemoveRightSideItem(int id, out ShopEntry entry)
        {
            for (var i = 0; i < RightSideItems.Count; i++)
            {
                if (RightSideItems[i].ItemId == id)
                {
                    var existing = RightSideItems[i];
                    RightSideItems.RemoveAt(i);
                    entry = existing;
                    return true;
                }
            }

            entry = new ShopEntry() { ItemId = -1, Count = 0 };
            return false;
        }

        public void FinalizeDropWithCount(ShopDragData dragData, int count)
        {
            if (count <= 0 || count > 30000)
                return;
            
            if (dragData.DragSource == ItemListRole.BuyFromNpcItemList)
            {
                var item = ClientDataLoader.Instance.GetItemById(dragData.ItemId);
                var leftItem = LeftSideItems[dragData.ItemId];
                leftItem.Count = count;
                RightWindow.AddItemEntry(leftItem, !item.IsUnique);
                if (!item.IsUnique && RemoveRightSideItem(dragData.ItemId, out var existing))
                    leftItem.Count += existing.Count;
                RightSideItems.Add(leftItem);
            }

            if (dragData.DragSource == ItemListRole.BuyFromNpcSummary)
            {
                if (RemoveRightSideItem(dragData.ItemId, out var entry))
                {
                    if (entry.Count - count <= 0)
                        RightWindow.RemoveItemEntry(entry);
                    else
                    {
                        entry.Count -= count;
                        RightWindow.UpdateExistingCount(entry.ItemId, entry.Count);
                        RightSideItems.Add(entry); //re-add it with lower count
                    }
                }
            }

            if (dragData.DragSource == ItemListRole.SellToNpcItemList)
            {
                var leftItem = LeftSideItems[dragData.ItemId];
                if (count > leftItem.Count)
                    return;
                
                var origCount = leftItem.Count;
                leftItem.Count -= count;
                if (leftItem.Count <= 0)
                {
                    LeftSideItems.Remove(dragData.ItemId);
                    LeftWindow.RemoveItemEntry(leftItem);
                }
                else
                {
                    LeftSideItems[dragData.ItemId] = leftItem;
                    LeftWindow.UpdateExistingCount(leftItem.ItemId, leftItem.Count);
                }

                if (RemoveRightSideItem(dragData.ItemId, out var entry))
                {
                    entry.Count += count;
                    RightSideItems.Add(entry);
                    RightWindow.UpdateExistingCount(entry.ItemId, entry.Count);
                }
                else
                {
                    var item = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(dragData.ItemId);
                    entry = new ShopEntry() { ItemId = dragData.ItemId, Cost = item.SalePrice, Count = count };
                    RightSideItems.Add(entry);
                    RightWindow.AddItemEntry(item, entry);
                }
            }
            
            if (dragData.DragSource == ItemListRole.SellToNpcSummary)
            {
                if (!RemoveRightSideItem(dragData.ItemId, out var entry))
                    return;
                if (entry.Count - count < 0)
                {
                    RightSideItems.Add(entry); //readd it, it's not moving anywhere
                    return;
                }

                if (LeftSideItems.ContainsKey(entry.ItemId))
                {
                    var leftItem = LeftSideItems[dragData.ItemId];
                    leftItem.Count += count;
                    LeftSideItems[dragData.ItemId] = leftItem;
                    LeftWindow.UpdateExistingCount(leftItem.ItemId, leftItem.Count);
                }
                else
                {
                    var item = NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(dragData.ItemId);
                    var newLeftEntry = new ShopEntry() { ItemId = dragData.ItemId, Cost = item.SalePrice, Count = count };
                    LeftSideItems.Add(entry.ItemId, newLeftEntry);
                    LeftWindow.AddItemEntry(item, newLeftEntry);
                }

                entry.Count -= count;
                if (entry.Count == 0)
                {
                    RightWindow.RemoveItemEntry(entry);
                }
                else
                {
                    RightWindow.UpdateExistingCount(entry.ItemId, entry.Count);
                    RightSideItems.Add(entry);
                }
            }
            
            RefreshRightSideCost();
        }

        public void RefreshRightSideCost()
        {
            var cost = 0;
            var weight = 0;
            foreach (var i in RightSideItems)
            {
                var item = ClientDataLoader.Instance.GetItemById(i.ItemId);
                cost += i.Cost * i.Count;
                weight += i.Count * item.Weight;
            }

            var errMessage = "";
            var state = NetworkManager.Instance.PlayerState;
            if (isBuyingFromNpc && state.CurrentWeight + weight > state.MaxWeight)
                errMessage = " <color=red>(Too Heavy!)</color>";
            
            //do zeny check here
            if (isBuyingFromNpc && cost > state.Zeny)
            {
                RightWindow.InfoAreaText.text = $"Total: <color=red>{cost:N0} Zeny (Short {(cost - state.Zeny):N0}z)</color>";
                RightWindow.OkButton.interactable = false;
            }
            else
            {
                RightWindow.InfoAreaText.text = $"Total: {cost:N0} Zeny{errMessage}";
                if (errMessage == "" && RightSideItems.Count > 0)
                    RightWindow.OkButton.interactable = true;
                else
                    RightWindow.OkButton.interactable = false;
                
            }
        }

        public static ShopUI InitializeShopUI(GameObject genericItemListPrefab, RectTransform parentContainer)
        {
            var left = Instantiate(genericItemListPrefab, parentContainer);
            var right = Instantiate(genericItemListPrefab, parentContainer);
            var shop = left.AddComponent<ShopUI>();
            Instance = shop;

            shop.LeftWindow = left.GetComponent<ItemListWindow>();
            shop.RightWindow = right.GetComponent<ItemListWindow>();
            
            return shop;
        }

        public void CenterShopUI()
        {
            var rectLeft = LeftWindow.GetComponent<RectTransform>();
            var rectRight = RightWindow.GetComponent<RectTransform>();

            var parentContainer = (RectTransform)transform.parent;
            var middle = parentContainer.rect.size / 2f;
            middle = new Vector2(middle.x, -middle.y); //flip y

            var sizeLeft = rectLeft.sizeDelta;
            var sizeRight = rectRight.sizeDelta;
            
            rectRight.sizeDelta = new Vector2(sizeLeft.x, sizeLeft.y  * 0.6f);

            rectLeft.anchoredPosition = middle - new Vector2(sizeLeft.x, -sizeLeft.y / 2);
            rectRight.anchoredPosition = middle - new Vector2(0, sizeLeft.y / 2 - rectRight.sizeDelta.y);
        }

        public void BeginBuyFromNpc(List<ShopEntry> items)
        {
            isBuyingFromNpc = true;
            LeftSideItems.Clear();

            LeftWindow.Prepare(ItemListRole.BuyFromNpcItemList);
            RightWindow.Prepare(ItemListRole.BuyFromNpcSummary);

            RightWindow.OnPressOk = OnFinish;
            RightWindow.OnPressCancel = OnCancel;

            foreach (var i in items)
            {
                LeftSideItems.TryAdd(i.ItemId, i); //some npcs sell the same item more than once, so only track one
                LeftWindow.AddItemEntry(i, false);
            }
            
            CenterShopUI();
            RightWindow.OkButton.interactable = false;
            LeftWindow.MoveToTop();
        }

        public void BeginSellToNpc()
        {
            var rectLeft = LeftWindow.GetComponent<RectTransform>();
            var rectRight = RightWindow.GetComponent<RectTransform>();

            rectLeft.anchoredPosition = new Vector2(-99999,-99999);
            rectRight.anchoredPosition = new Vector2(-99999,-99999);

            StartCoroutine(BeginSellToNpcCoroutine());
        }

        //this causes big frame drops if you've got an inventory with more than 100 items, so we're gonna gamble that 
        private IEnumerator BeginSellToNpcCoroutine()
        {
            LeftWindow.Prepare(ItemListRole.SellToNpcItemList);
            RightWindow.Prepare(ItemListRole.SellToNpcSummary);
            
            RightWindow.OnPressOk = OnFinish;
            RightWindow.OnPressCancel = OnCancel;

            var state = NetworkManager.Instance.PlayerState;
            var inventory = state.Inventory;
            var count = 0;
            
            foreach (var i in inventory.GetInventoryData())
            {
                if (count > 15)
                {
                    yield return null;
                    count = 0;
                }

                count++;
                var item = i.Value;
                if (item.Type == ItemType.UniqueItem && state.EquippedBagIdHashes.Contains(item.BagSlotId))
                    continue;
                var shopEntry = new ShopEntry() { ItemId = item.BagSlotId, Count = item.Count, Cost = item.SalePrice };
                LeftSideItems.Add(shopEntry.ItemId, shopEntry);
                LeftWindow.AddItemEntry(item, shopEntry);
            }
            
            CenterShopUI();
            RightWindow.OkButton.interactable = false;
            LeftWindow.MoveToTop();
        }
    }
}
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Inventory
{
    public struct VendingEntry
    {
        public InventoryItem Item;
        public int Price;
    }

    public class VendingShopViewUI : MonoBehaviour
    {
        public static VendingShopViewUI ActiveTradeWindow;
        public TextMeshProUGUI LeftTitle;

        public GenericItemListV2 LeftWindow;
        public GenericItemListV2 RightWindow;

        private Dictionary<int, ItemListEntryV2> leftEntries = new();
        private Dictionary<int, ItemListEntryV2> rightEntries = new();

        private Dictionary<int, VendingEntry> itemList;

        public void CloseShop()
        {
            NetworkManager.Instance.VendingEnd();
            if (LeftWindow != null)
                Destroy(LeftWindow.gameObject);
            if (RightWindow != null)
                Destroy(RightWindow.gameObject);
            Destroy(gameObject);
            ActiveTradeWindow = null;
        }

        public void SubmitPurchase()
        {
            NetworkManager.Instance.SubmitVendingPurchase(rightEntries);
            CloseShop();
        }

        public void OnStartDrag(DragItemType type)
        {
            if(type == DragItemType.VendPurchase)
                LeftWindow.DropZone.gameObject.SetActive(true);
            if(type == DragItemType.VendShop)
                RightWindow.DropZone.gameObject.SetActive(true);
        }

        public void OnStopDrag()
        {
            LeftWindow.DropZone.gameObject.SetActive(false);
            RightWindow.DropZone.gameObject.SetActive(false);
        }

        public void OnRightClickEntry(int bagId)
        {
            if (!itemList.TryGetValue(bagId, out var item))
                return;
            
            UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(item.Item);
        }

        private void OnDropItemOntoLeftSide(ItemDragObject srcItem) => OnDropItemOntoLeftSide(srcItem.OriginId);
        private void OnDropItemOntoLeftSide(int bagId)
        {
            var leftEntry = rightEntries[bagId];
            
            if (leftEntry.ItemCount == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                FinalizeDropItemOntoLeftSide(bagId, leftEntry.ItemCount);
            else
            {
                var item = itemList[bagId];
                UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item.Item, $"Move {item.Item.ProperName()}", FinalizeDropItemOntoLeftSide, leftEntry.ItemCount);
            }
        }
        
        private void OnDropItemOntoRightSide(ItemDragObject srcItem) => OnDropItemOntoRightSide(srcItem.OriginId, srcItem.ItemCount);
        private void OnDropItemOntoRightSide(int bagId, int count)
        {
            if (count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                FinalizeDropItemOntoRightSide(bagId, count);
            else
            {
                var item = itemList[bagId].Item;
                item.Count = count;
                UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, $"Move {item.ProperName()}", FinalizeDropItemOntoRightSide);
            }
        }
        
        public void FinalizeDropItemOntoLeftSide(int bagId, int count)
        {
            if (count <= 0)
                return;
            
            var rightItem = rightEntries[bagId];

            count = Mathf.Clamp(count, 1, rightItem.ItemCount);
            var leftCount = count;

            if (leftEntries.TryGetValue(bagId, out var entry))
            {
                leftCount += entry.ItemCount;
                entry.UpdateCount(leftCount);
            }
            else
            {
                entry = LeftWindow.GetNewEntry();

                entry.Assign(DragItemType.VendShop, rightItem.Sprite, rightItem.ItemId, leftCount);
                entry.UniqueEntryId = rightItem.UniqueEntryId;
                entry.ItemName.text = rightItem.ItemName.text;
                entry.RightText.gameObject.SetActive(true);
                entry.DragOrigin = ItemDragOrigin.VendingSource;
                // entry.RightText.text = $"{item.Price:N0}z";
                entry.CanSelect = false;
                entry.CanDrag = true;
                //entry.EventOnSelect = OnSelect;
                entry.EventOnRightClick = OnRightClickEntry;
                
                leftEntries.Add(bagId, entry);
            }

            //var item = itemList[bagId];
            var newCount = rightItem.ItemCount - count;

            if (newCount > 0)
            {
                var item = itemList[bagId];
                var price = item.Price * newCount;
                rightItem.UpdateCount(newCount);
                rightItem.RightText.text = $"{price:N0}z";
            }
            else
            {
                RightWindow.ReturnItemListEntry(rightItem);
                rightEntries.Remove(bagId);
            }
            
            RefreshRightSideCost();
        }

        public void FinalizeDropItemOntoRightSide(int bagId, int count)
        {
            if (count <= 0)
                return;
            
            var leftItem = leftEntries[bagId];
            
            count = Mathf.Clamp(count, 1, leftItem.ItemCount);
            var rightCount = count;

            if (rightEntries.TryGetValue(bagId, out var entry))
            {
                rightCount += entry.ItemCount;
                entry.UpdateCount(rightCount);
            }
            else
            {
                entry = RightWindow.GetNewEntry();

                entry.Assign(DragItemType.VendPurchase, leftItem.Sprite, leftItem.ItemId, rightCount);
                entry.UniqueEntryId = leftItem.UniqueEntryId;
                entry.ItemName.text = leftItem.ItemName.text;
                entry.RightText.gameObject.SetActive(true);
                entry.DragOrigin = ItemDragOrigin.VendingTarget;
                // entry.RightText.text = $"{item.Price:N0}z";
                entry.CanSelect = false;
                entry.CanDrag = true;
                //entry.EventOnSelect = OnSelect;
                entry.EventOnRightClick = OnRightClickEntry;
                
                rightEntries.Add(bagId, entry);
            }

            var item = itemList[bagId];
            var price = item.Price * rightCount;
            entry.RightText.text = $"{price:N0}z";

            if (leftItem.ItemCount > count)
                leftItem.UpdateCount(leftItem.ItemCount - count);
            else
            {
                LeftWindow.ReturnItemListEntry(leftItem);
                leftEntries.Remove(bagId);
            }
            
            RefreshRightSideCost();
        }

        public void RefreshRightSideCost()
        {
            var cost = 0;
            var weight = 0;
            foreach (var (bagId, entry) in rightEntries)
            {
                var item = itemList[bagId];

                cost += item.Price * entry.ItemCount;
                weight += item.Item.ItemData.Weight * entry.ItemCount;
            }

            var errMessage = "";
            var state = NetworkManager.Instance.PlayerState;
            if (state.CurrentWeight + weight > state.MaxWeight)
                errMessage = " <color=red>(Too Heavy!)</color>";
            
            //do zeny check here
            if (cost > state.Zeny)
            {
                RightWindow.InfoAreaText.text = $"Total: <color=red>{cost:N0} Zeny (Short {(cost - state.Zeny):N0}z)</color>";
                RightWindow.OkButton.interactable = false;
            }
            else
            {
                RightWindow.InfoAreaText.text = $"Total: {cost:N0} Zeny{errMessage}";
                if (errMessage == "" && rightEntries.Count > 0)
                    RightWindow.OkButton.interactable = true;
                else
                    RightWindow.OkButton.interactable = false;
            }
        }

        public static void StartViewVendingShop(string shopName, Dictionary<int, VendingEntry> sellingList)
        {
            var go = Instantiate(Resources.Load<GameObject>("VendingItemStore"), UiManager.Instance.PrimaryUserWindowContainer);
            go.SetActive(true);

            ActiveTradeWindow = go.GetComponent<VendingShopViewUI>();

            ActiveTradeWindow.itemList = sellingList;
            ActiveTradeWindow.LeftTitle.text = shopName;
            ActiveTradeWindow.Init();
        }

        private void Init()
        {
            LeftWindow.transform.SetParent(UiManager.Instance.PrimaryUserWindowContainer);
            RightWindow.transform.SetParent(UiManager.Instance.PrimaryUserWindowContainer);
            LeftWindow.MoveToTop();
            RightWindow.MoveToTop();

            LeftWindow.DropZone.OnDropItem = OnDropItemOntoLeftSide;
            RightWindow.DropZone.OnDropItem = OnDropItemOntoRightSide;

            foreach(var (bagId, item) in itemList)
            {
                var entry = LeftWindow.GetNewEntry();

                var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(item.Item.ItemData.Sprite);
                entry.Assign(DragItemType.VendShop, sprite, item.Item.Id, item.Item.Count);
                entry.UniqueEntryId = bagId;
                entry.ItemName.text = item.Item.ProperName();
                entry.RightText.gameObject.SetActive(true);
                entry.RightText.text = $"{item.Price:N0}z";
                entry.DragOrigin = ItemDragOrigin.VendingSource;
                entry.CanSelect = false;
                entry.CanDrag = true;
                //entry.EventOnSelect = OnSelect;
                entry.EventOnRightClick = OnRightClickEntry;

                leftEntries.Add(bagId, entry);
            }
            
            RightWindow.InfoAreaText.text = $"Total: 0 Zeny";
        }

        private void OnDestroy()
        {
            if (LeftWindow != null)
                Destroy(LeftWindow.gameObject);
            if (RightWindow != null)
                Destroy(RightWindow.gameObject);
            ActiveTradeWindow = null;
        }
    }
}
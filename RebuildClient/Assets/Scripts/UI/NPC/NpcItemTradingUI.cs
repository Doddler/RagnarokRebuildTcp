using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.TitleScreen;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public record ItemTrade(InventoryItem Item, int Count, int ZenyCost, List<RegularItem> Requirements);
    
    public class NpcItemTradingUI : MonoBehaviour
    {
        public static NpcItemTradingUI ActiveTradeWindow;
        
        public GenericItemListV2 LeftWindow;
        public GenericItemListV2 RightWindow;

        public Button CountUpButton;
        public Button CountDownButton;
        public TextMeshProUGUI CountText;
        
        private GenericItemListV2 selectWindow;
        
        private List<ItemTrade> tradeItems;
        private List<ItemListEntryV2> leftEntries = new();
        private List<ItemListEntryV2> rightEntries = new();
        private Dictionary<int, ItemTrade> tradeReferences = new();

        private int currentTradeId = -1;
        private ItemTrade currentTrade;
        private int maxCount;
        private int curCount;
        
        private List<int> bagIds = null;
        private HashSet<int> uniqueItems;
        private Dictionary<int, ItemListEntryV2> selectEntries;
        
        private int selectedBagId;

        public void OnCancel()
        {
            NetworkManager.Instance.SendCancelTrade();
            if(LeftWindow != null)
                Destroy(LeftWindow.gameObject);
            if(RightWindow != null)
                Destroy(RightWindow.gameObject);
            if(selectWindow != null)
                Destroy(selectWindow.gameObject);
            Destroy(gameObject);
        }

        public void OnRightClickLeftEntry(int id)
        {
            var entry = leftEntries[id];
            UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(tradeItems[id].Item);
            entry.ResolveLeftClick();
        }
        
        
        public void OnRightClickRightEntry(int id)
        {
            var entry = rightEntries[id];
            UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(new InventoryItem(new RegularItem() {Id = entry.ItemId}));
            
        }

        public void CountUp() => OnChangeCount(1);

        public void CountDown() => OnChangeCount(-1);

        private void OnChangeCount(int change)
        {
            var count = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 10 : 1;
            curCount += count * change;

            if (curCount < 1)
                curCount = 1;
            if (curCount > maxCount)
                curCount = maxCount;

            CountUpButton.interactable = curCount < maxCount;
            CountDownButton.interactable = curCount > 1;

            CountText.text = curCount.ToString();

            var cost = currentTrade.ZenyCost * curCount;
            
            if (cost > 0)
                RightWindow.InfoAreaText.text = $"Cost: {cost:N0}z";
            else
                RightWindow.InfoAreaText.text = "";

        }

        public void OnSubmitTrade()
        {
            LeftWindow.gameObject.SetActive(false); 
            RightWindow.gameObject.SetActive(false); //dangerous, can softlock if the trade fails without the client responding
            bagIds?.Clear();
            CheckAndTryTrade();
        }

        public void CheckAndTryTrade()
        {
            var inventory = PlayerState.Instance.Inventory;
            var equipHashes = PlayerState.Instance.EquippedBagIdHashes;
            
            //if they have unique items to trade, need to make sure they trade the right ones
            foreach (var req in currentTrade.Requirements)
            {
                if (uniqueItems != null && uniqueItems.Contains(req.Id))
                    continue;

                if (inventory.TryGetInventoryItem(req.Id, out _))
                    continue;
                
                PromptForItem(req.Id);
                return;
            }
            
            NetworkManager.Instance.SendCompleteNpcTrade(currentTradeId, curCount, bagIds);
            // OnCancel(); //add submit here
        }
        
        public void PromptForItem(int itemId)
        {
            var prefab = UiManager.Instance.GenericItemListV2Prefab;
            var container = UiManager.Instance.PrimaryUserWindowContainer;
            var go = Instantiate(prefab, container);
            selectWindow = go.GetComponent<GenericItemListV2>();
            selectWindow.MoveToTop();
            selectWindow.CenterWindow();
            selectWindow.ToggleBox.gameObject.SetActive(false);
            selectWindow.InfoAreaText.gameObject.SetActive(false);
            selectWindow.OnPressCancel = OnCancel;

            var equipHashes = PlayerState.Instance.EquippedBagIdHashes;
            var inventory = PlayerState.Instance.Inventory;
            selectWindow.TitleBar.text = "Select Item to Trade";
            selectWindow.OkButtonText.text = "Select";
            selectWindow.OkButton.interactable = false;
            selectWindow.OnPressOk = FinishSelectUniqueItemForSubmit;
            selectWindow.SetActive();

            selectEntries = new();
            selectedBagId = -1;

            foreach (var (_, item) in inventory.GetInventoryData())
            {
                if (item.Id != itemId)
                    continue;
                
                var entry = selectWindow.GetNewEntry();
                
                var data = ClientDataLoader.Instance.GetItemById(itemId);
                var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(data.Sprite);
                entry.Assign(DragItemType.None, sprite, itemId, 1);
                entry.ItemName.text = item.ProperName();
                entry.RightText.text = "";
                entry.CanSelect = true;
                entry.EventOnSelect = OnClickItemOnPromptList;
                entry.UniqueEntryId = item.BagSlotId;
                //entry.EventDoubleClick = DoubleClickItemOnPromptList;
                if (!equipHashes.Contains(item.BagSlotId))
                    entry.HideCount();
                else
                    entry.CountText.text = "E";

                selectEntries.Add(item.BagSlotId, entry);
            }
        }

        public void DoubleClickItemOnPromptList(int bagId)
        {
            OnClickItemOnPromptList(bagId);
            FinishSelectUniqueItemForSubmit();
        }

        public void OnClickItemOnPromptList(int bagId)
        {
            if(selectedBagId > 0 && bagId != selectedBagId)
                selectEntries[selectedBagId].Unselect();
            
            selectedBagId = bagId;
            selectWindow.OkButton.interactable = true;
        }

        public void FinishSelectUniqueItemForSubmit()
        {
            if (!PlayerState.Instance.Inventory.TryGetInventoryItem(selectedBagId, out var item))
            {
                Debug.Log($"Could not find selected item {selectedBagId}!");
                OnCancel();
                return;
            }

            uniqueItems ??= new HashSet<int>();
            uniqueItems.Add(item.Id);
            bagIds ??= new List<int>();
            bagIds.Add(selectedBagId);
            
            Destroy(selectWindow.gameObject);

            if (item.UniqueItem.Refine > 0)
            {
                UiManager.Instance.YesNoOptionsWindow.BeginPrompt($"{item.ProperName()} - This item is refined, are you sure you want to trade it?",
                    "Yes", "No", CheckAndTryTrade, OnCancel, false, false);
                return;
            }
            
            if (item.UniqueItem.SlotData(0) > 0)
            {
                UiManager.Instance.YesNoOptionsWindow.BeginPrompt($"{item.ProperName()} - This item has cards socketed, are you sure you want to trade it?",
                    "Yes", "No", CheckAndTryTrade, OnCancel, false, false);
                return;
            }
            
            if (PlayerState.Instance.EquippedBagIdHashes.Contains(selectedBagId))
            {
                UiManager.Instance.YesNoOptionsWindow.BeginPrompt($"{item.ProperName()} - This item is currently equipped, are you sure you want to trade it?",
                    "Yes", "No", CheckAndTryTrade, OnCancel, false, false);
                return;
            }
            
            CheckAndTryTrade();
        }

        public void OnSelect(int tradeItemId)
        {
            if(currentTradeId >= 0 && tradeItemId != currentTradeId)
                leftEntries[currentTradeId].Unselect();
            
            currentTradeId = tradeItemId;
            currentTrade = tradeReferences[currentTradeId];
            
            foreach(var e in rightEntries)
                RightWindow.ReturnItemListEntry(e);
            
            rightEntries.Clear();

            var canAfford = PlayerState.Instance.Zeny >= currentTrade.ZenyCost; 
            var maxAfford = 99;
            var inventory = PlayerState.Instance.Inventory;
            foreach (var req in currentTrade.Requirements)
            {
                var entry = RightWindow.GetNewEntry();
                
                var data = ClientDataLoader.Instance.GetItemById(req.Id);
                var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(data.Sprite);
                entry.Assign(DragItemType.None, sprite, req.Id, req.Count);
                
                entry.ItemName.text = InventoryItem.MakeProperName(req, data);
                entry.RightText.gameObject.SetActive(false);
                entry.UniqueEntryId = rightEntries.Count;
                entry.CanSelect = false;
                entry.CanDrag = false;
                entry.EventOnRightClick = OnRightClickRightEntry;
                
                var onHand = inventory.CountItemByItemId(req.Id);
                if (onHand < req.Count)
                {
                    entry.RedCount();
                    canAfford = false;
                    maxAfford = 1;
                }

                if (data.IsUnique)
                    maxAfford = 1;
                else
                {
                    var itemAfford = onHand / req.Count;
                    if (itemAfford < maxAfford)
                        maxAfford = itemAfford;
                }
                

                rightEntries.Add(entry);
            }

            RightWindow.OkButton.interactable = canAfford;

            CountText.text = canAfford ? "1" : "-";
            maxCount = maxAfford;
            curCount = 1;
            
            if (currentTrade.ZenyCost > 0)
                RightWindow.InfoAreaText.text = $"Cost: {currentTrade.ZenyCost:N0}z";
            else
                RightWindow.InfoAreaText.text = "";

            if (maxAfford > 1)
            {
                CountUpButton.interactable = true;
            }
        }

        public static void StartNpcTrade(List<ItemTrade> tradeItems)
        {
            var go = GameObject.Instantiate(UiManager.Instance.NpcTradePrefab, UiManager.Instance.PrimaryUserWindowContainer);
            go.SetActive(true);
            
            ActiveTradeWindow = go.GetComponent<NpcItemTradingUI>();

            ActiveTradeWindow.tradeItems = tradeItems;
            ActiveTradeWindow.Init();
        }

        private void Init()
        {
            LeftWindow.transform.SetParent(UiManager.Instance.PrimaryUserWindowContainer);
            RightWindow.transform.SetParent(UiManager.Instance.PrimaryUserWindowContainer);
            LeftWindow.MoveToTop();
            RightWindow.MoveToTop();
            
            
            maxCount = 0;
            CountText.text = "-";
            CountUpButton.interactable = false;
            CountDownButton.interactable = false;
            LeftWindow.HasSubmitButtons = false;
            LeftWindow.OnPressCancel = OnCancel;
            LeftWindow.OnCloseWindow = OnCancel;
            RightWindow.OkButtonText.text = "Trade";
            RightWindow.HasSubmitButtons = true;
            RightWindow.OkButton.interactable = false;
            RightWindow.OnPressOk = OnSubmitTrade;
            RightWindow.OnPressCancel = OnCancel;
            RightWindow.OnCloseWindow = OnCancel;
            RightWindow.InfoAreaText.text = "";
            
            LeftWindow.Init();
            RightWindow.Init();

            
            for (var i = 0; i < tradeItems.Count; i++)
            {
                var trade = tradeItems[i];

                var entry = LeftWindow.GetNewEntry();
                
                var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(trade.Item.ItemData.Sprite);
                entry.Assign(DragItemType.None, sprite, trade.Item.Id, trade.Item.Count);
                entry.UniqueEntryId = i;
                entry.ItemName.text = trade.Item.ProperName();
                entry.RightText.gameObject.SetActive(false);
                entry.CanSelect = true;
                entry.CanDrag = false;
                entry.EventOnSelect = OnSelect;
                entry.EventOnRightClick = OnRightClickLeftEntry;
                
                leftEntries.Add(entry);
                tradeReferences.Add(i, trade);
            }

        }

        private void OnDestroy()
        {
            if(LeftWindow != null)
                Destroy(LeftWindow.gameObject);
            if(RightWindow != null)
                Destroy(RightWindow.gameObject);
            if(selectWindow != null)
                Destroy(selectWindow);
        }
    }
}
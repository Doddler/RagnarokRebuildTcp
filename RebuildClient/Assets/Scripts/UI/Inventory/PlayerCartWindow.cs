using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Inventory
{
    public class PlayerCartWindow : WindowBase
    {
        public Vector2 UnitSize;
        public RectTransform ItemBoxRoot;
        public RectTransform ViewBoxTransform;
        public GameObject ItemEntryPrefab;
        public ScrollRect ScrollArea;
        public CartDropZone DropZone;
        private List<InventoryEntry> entryList = new();
        public TextMeshProUGUI WeightText;
        private int activeEntryCount;
        private bool isInit;
        
        public void Awake()
        {
            if (ItemBoxRoot.childCount != 0)
                Destroy(ItemBoxRoot.transform.GetChild(0).gameObject);
        }

        public void UpdateDropArea(bool isActive)
        {
            DropZone.DisableDropArea();
            DropZone.gameObject.SetActive(isActive);
        }

        public override void ShowWindow()
        {
            base.ShowWindow();
            UpdateActiveVisibleBag();
        }
        
        public void OnMoveInventoryItemToCart(int bagSlotId)
        {
            if (VendingSetupManager.Instance != null)
            {
                CameraFollower.Instance.AppendNotice("You can't transfer items with your cart while setting up a vend.");
                return;
            }
            
            Debug.Log($"Moving item to cart: {bagSlotId}");

            if (NetworkManager.Instance.PlayerState.Inventory.GetInventoryData().TryGetValue(bagSlotId, out var item))
            {
                if (item.ItemData.IsUnique || item.Count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    NetworkManager.Instance.CartItemInteraction(CartInteractionType.InventoryToCart, bagSlotId, item.Count);
                else
                    UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.InventoryToCart);
            }
        }
        
        public void OnMoveCartItemToInventory(int bagSlotId)
        {
            if (VendingSetupManager.Instance != null)
            {
                CameraFollower.Instance.AppendNotice("You can't transfer items with your cart while setting up a vend.");
                return;
            }
            
            Debug.Log($"Moving item to storage: {bagSlotId}");

            if (NetworkManager.Instance.PlayerState.Cart.GetInventoryData().TryGetValue(bagSlotId, out var item))
            {
                if (item.ItemData.IsUnique || item.Count == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    NetworkManager.Instance.CartItemInteraction(CartInteractionType.CartToInventory, bagSlotId, item.Count);
                else
                    UiManager.Instance.DropCountConfirmationWindow.BeginItemDrop(item, DropConfirmationType.CartToInventory);
            }
        }

        private void OnRightClick(InventoryItem item)
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                OnMoveCartItemToInventory(item.BagSlotId);
                return;
            }

            UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(item);
        }
        
        private void OnDoubleClick(InventoryItem item)
        {
            var state = NetworkManager.Instance.PlayerState;
        
            if (UiManager.Instance.InventoryWindow.gameObject.activeInHierarchy)
            {
                OnMoveCartItemToInventory(item.BagSlotId);
                return;
            }
        }
        
        public void UpdateActiveVisibleBag()
        {
            if (!gameObject.activeSelf || NetworkManager.Instance?.PlayerState == null)
                return;

            var state = NetworkManager.Instance.PlayerState;
            var inventory = state.Cart;
            activeEntryCount = 0;
            var bagItems = inventory.GetInventoryData();
            var curWeight = state.CartWeight / 10;
            var totalWeight = 8000;

            var weightPercent = curWeight * 100 / totalWeight;
            var countText = $"{bagItems.Count}/100";
            var percentText = $"{weightPercent}%";
            
            WeightText.text = $"Items: {countText}  Weight: {curWeight}/{totalWeight} ({percentText})";
            
            foreach (var bagEntry in bagItems)
            {
                var item = bagEntry.Value;


                if (entryList.Count <= activeEntryCount)
                {
                    var newEntry = GameObject.Instantiate(ItemEntryPrefab, ItemBoxRoot, false);
                    var drag = newEntry.GetComponent<InventoryEntry>();
                    entryList.Add(drag);
                }

                var itemEntry = entryList[activeEntryCount];
                var sprite = ClientDataLoader.Instance.GetIconAtlasSprite(item.ItemData.Sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite {item.ItemData.Sprite} for item {item.ItemData.Name}");
                    sprite = ClientDataLoader.Instance.GetIconAtlasSprite("Apple");
                }

                itemEntry.DragItem.Assign(DragItemType.CartItem, sprite, bagEntry.Key, item.Count);
                itemEntry.DragItem.Origin = ItemDragOrigin.CartWindow;
                itemEntry.DragItem.OriginId = bagEntry.Key;
                itemEntry.gameObject.SetActive(true);
                itemEntry.DragItem.gameObject.SetActive(true);
                itemEntry.DragItem.OnRightClick = () => OnRightClick(item); //captures like this allocate a lot, don't they? Hmm...
                // if (state.EquippedItems.Contains(item.BagSlotId))
                // {
                //     itemEntry.DragItem.SetEquipped();
                //     itemEntry.DragItem.OnDoubleClick = null;
                // }
                // else
                itemEntry.DragItem.OnDoubleClick = () => OnDoubleClick(item);

                activeEntryCount++;
            }
            
            var left = ItemBoxRoot.anchoredPosition;
            var entriesPerRow = (int)((ViewBoxTransform.rect.width - 40 + 5 - left.x) / UnitSize.x); //the 5 is cause there's no spacing after the last item in a row
            var requiredRows = Mathf.CeilToInt((float)activeEntryCount / entriesPerRow);
            var requiredBoxes = entriesPerRow * requiredRows;

            var minRows = (int)((ViewBoxTransform.rect.height - 8) / UnitSize.y);
            var minBoxes = entriesPerRow * minRows;
            var totalBoxes = Mathf.Max(requiredBoxes, minBoxes);
            
            // Debug.Log($"TotalBoxes: {totalBoxes} EntryList.Count: {entryList.Count} ActiveEntryCount: {activeEntryCount}");

            while (totalBoxes > entryList.Count)
            {
                var newEntry = GameObject.Instantiate(ItemEntryPrefab, ItemBoxRoot, false);
                var drag = newEntry.GetComponent<InventoryEntry>();
                entryList.Add(drag);
            }
            
            // Debug.Log($"EntriesPerRow:{entriesPerRow} RequiredRows:{requiredRows} RequiredBoxes:{requiredBoxes} MinBoxes:{minBoxes} MinRows: {minRows} Rect:{ViewBoxTransform.rect}");

            for (var i = activeEntryCount; i < entryList.Count; i++)
            {
                entryList[i].DragItem.Clear();
                entryList[i].gameObject.SetActive(i < totalBoxes);
            }
        }
    }
}
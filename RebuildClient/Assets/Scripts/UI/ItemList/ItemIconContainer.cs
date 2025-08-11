using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.UI.Inventory;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class ItemIconContainer : MonoBehaviour
    {
        public Vector2 UnitSize;
        public RectTransform ItemBoxRoot;
        public RectTransform ViewBoxTransform;
        [SerializeField] private GameObject ItemEntryPrefab;
        public ItemListDropZoneV2 DropZone;
        public ItemDragOrigin DragOriginType;
        private List<InventoryEntry> entryList = new();
        private DragItemType dragType;
        private int activeEntryCount;
        private bool isInit;

        public Dictionary<int, InventoryItem> ItemList;
        
        public void Awake()
        {
            if (ItemBoxRoot.childCount != 0)
                ItemBoxRoot.transform.GetChild(0).gameObject.SetActive(false);
        }

        public void UpdateDropArea(bool isActive)
        {
            DropZone.DisableDropArea();
            DropZone.gameObject.SetActive(isActive);
        }
        

        private void OnRightClick(InventoryItem item)
        {
            UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(item);
        }
        
        private void OnDoubleClick(InventoryItem item)
        {
            var state = NetworkManager.Instance.PlayerState;
        
            if (UiManager.Instance.InventoryWindow.gameObject.activeInHierarchy)
            {
                // OnMoveCartItemToInventory(item.BagSlotId);
                return;
            }
        }

        public void AssignItemList(Dictionary<int, InventoryItem> newItemList, DragItemType listDragType)
        {
            ItemList = newItemList;
            dragType = listDragType;
            RefreshItemList();
        }
        
        public void RefreshItemList()
        {
            if (!gameObject.activeSelf)
                return;

            activeEntryCount = 0;
            
            foreach (var bagEntry in ItemList)
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

                itemEntry.DragItem.Assign(dragType, sprite, bagEntry.Key, item.Count);
                itemEntry.DragItem.Origin = DragOriginType;
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
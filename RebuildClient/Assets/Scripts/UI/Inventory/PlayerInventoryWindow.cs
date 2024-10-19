using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Inventory
{
    public class PlayerInventoryWindow : WindowBase
    {
        public Button[] UiTabButtons;
        public GameObject[] UiButtonGroups;
        public Vector2 UnitSize;
        public RectTransform ItemBoxRoot;
        public RectTransform ViewBoxTransform;
        public GameObject ItemEntryPrefab;
        public ScrollRect ScrollArea;
        private List<InventoryEntry> entryList = new();
        public TextMeshProUGUI WeightText;
        private int activeEntryCount;
        private int activeItemSection;
        private bool isInit;

        public void Awake()
        {
            if (ItemBoxRoot.childCount != 0)
                Destroy(ItemBoxRoot.transform.GetChild(0).gameObject);
        }

        public override void ShowWindow()
        {
            base.ShowWindow();
            UpdateActiveVisibleBag();
        }
        
        public void ClickTabButton(int tab)
        {
            switch (tab)
            {
                case 0:
                    UiButtonGroups[1].transform.SetAsLastSibling();
                    UiButtonGroups[2].transform.SetAsLastSibling();
                    UiButtonGroups[0].transform.SetAsLastSibling();
                    break;
                case 1:
                    UiButtonGroups[0].transform.SetAsLastSibling();
                    UiButtonGroups[2].transform.SetAsLastSibling();
                    UiButtonGroups[1].transform.SetAsLastSibling();
                    break;
                case 2:
                    UiButtonGroups[0].transform.SetAsLastSibling();
                    UiButtonGroups[1].transform.SetAsLastSibling();
                    UiButtonGroups[2].transform.SetAsLastSibling();
                    break;
            }

            UiTabButtons[0].transform.localPosition = new Vector3(tab == 0 ? -30 : -24, UiTabButtons[0].transform.localPosition.y, 0);
            UiTabButtons[1].transform.localPosition = new Vector3(tab == 1 ? -30 : -24, UiTabButtons[1].transform.localPosition.y, 0);
            UiTabButtons[2].transform.localPosition = new Vector3(tab == 2 ? -30 : -24, UiTabButtons[2].transform.localPosition.y, 0);
            UiTabButtons[0].interactable = tab != 0;
            UiTabButtons[1].interactable = tab != 1;
            UiTabButtons[2].interactable = tab != 2;
            activeItemSection = tab;

            UpdateActiveVisibleBag();
        }
        
        public void UpdateActiveVisibleBag()
        {
            if (!gameObject.activeSelf || NetworkManager.Instance?.PlayerState == null)
                return;

            var state = NetworkManager.Instance.PlayerState;
            var inventory = state.Inventory;
            activeEntryCount = 0;
            var bagItems = inventory.GetInventoryData();
            var curWeight = state.CurrentWeight / 10;
            var totalWeight = state.MaxWeight / 10;

            var weightPercent = curWeight * 100 / totalWeight;
            var countText = bagItems.Count < 190 ? $"{bagItems.Count}/200" : $"<color=red>{bagItems.Count}</color>/200";
            var percentText = weightPercent < 90 ? $"{weightPercent}%" : $"<color=red>{weightPercent}%</color>";
            
            WeightText.text = $"Items: {countText}  Weight: {curWeight}/{totalWeight} ({percentText})";
            
            foreach (var bagEntry in bagItems)
            {
                var item = bagEntry.Value;
                if (activeItemSection == 0 && (item.ItemData.IsUnique || item.ItemData.UseType == ItemUseType.NotUsable)) continue;
                if (activeItemSection == 1 && (!item.ItemData.IsUnique || item.ItemData.Id < 0)) continue;
                if (activeItemSection == 2 && (item.ItemData.IsUnique || item.ItemData.UseType != ItemUseType.NotUsable) && item.ItemData.Id > 0) continue;

                if (entryList.Count <= activeEntryCount)
                {
                    var newEntry = GameObject.Instantiate(ItemEntryPrefab, ItemBoxRoot, false);
                    var drag = newEntry.GetComponent<InventoryEntry>();
                    entryList.Add(drag);
                }

                var itemEntry = entryList[activeEntryCount];
                var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(item.ItemData.Sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite {item.ItemData.Sprite} for item {item.ItemData.Name}");
                    sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite("Apple");
                }

                itemEntry.DragItem.Assign(DragItemType.Item, sprite, bagEntry.Key, item.Count);
                itemEntry.gameObject.SetActive(true);
                itemEntry.DragItem.gameObject.SetActive(true);

                if (item.ItemData.UseType == ItemUseType.Use)
                    itemEntry.DragItem.OnDoubleClick = () => NetworkManager.Instance.SendUseItem(bagEntry.Key);
                else if (item.ItemData.ItemClass == ItemClass.Weapon || item.ItemData.ItemClass == ItemClass.Equipment)
                    itemEntry.DragItem.OnDoubleClick = () => NetworkManager.Instance.SendEquipItem(bagEntry.Key);
                else
                    itemEntry.DragItem.OnDoubleClick = null;

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
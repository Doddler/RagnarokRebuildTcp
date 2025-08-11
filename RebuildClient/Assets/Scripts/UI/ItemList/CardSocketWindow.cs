using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI
{
    public class CardSocketWindow : MonoBehaviour
    {
        private static CardSocketWindow instance;
        private GenericItemListV2 cardWindow;
        private Dictionary<int, ItemListEntryV2> itemList;
        private ItemListEntryV2 selectedItem;
        private InventoryItem srcItem;

        public void SelectItem(int entryId)
        {
            if (selectedItem != null)
            {
                if (selectedItem.UniqueEntryId == entryId)
                    return;
                selectedItem.Unselect();
            }

            selectedItem = itemList[entryId];
            cardWindow.OkButton.interactable = true;
        }

        public void OnOk()
        {
            if(srcItem.Id != 0 && selectedItem != null)
                NetworkManager.Instance.SendSocketItem(selectedItem.ItemId, srcItem.Id);
            cardWindow.HideWindow();
            Destroy(gameObject);
        }

        public void OnCancel()
        {
            cardWindow.HideWindow();
            Destroy(gameObject);
        }

        public void OnDoubleClick(int id)
        {
            SelectItem(id);
            OnOk();
        }

        public void Init(GenericItemListV2 window, InventoryItem itemToSocket, List<InventoryItem> validCandidates)
        {
            cardWindow = window;
            selectedItem = null;
            srcItem = itemToSocket;
            itemList = new Dictionary<int, ItemListEntryV2>();

            window.HasSubmitButtons = true;
            window.HasToggleButton = false;
            // window.ShowItemValues = false;
            window.OkButton.interactable = false;
            window.OkButton.gameObject.SetActive(true);
            window.CancelButton.gameObject.SetActive(true);
            window.ToggleBox.gameObject.SetActive(false);
            window.InfoAreaText.gameObject.SetActive(false);
            window.OkButtonText.text = "Socket";
            window.TitleBar.text = $"Insert Card ({itemToSocket.ProperName()})";
            window.OnPressCancel = OnCancel;
            window.OnPressOk = OnOk;

            var state = NetworkManager.Instance.PlayerState;

            var id = 0;
            foreach (var item in validCandidates)
            {
                var sprite = ClientDataLoader.Instance.GetIconAtlasSprite(item.ItemData.Sprite);
                var entry = window.GetNewEntry();
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
                entry.EventDoubleClick = OnDoubleClick;
                itemList.Add(id, entry);
                id++;
            }

            window.Init();
            window.MoveToTop();
        }

        public static void BeginCardSocketing(InventoryItem itemToSocket)
        {
            if (instance != null)
                Destroy(instance.gameObject);

            if (itemToSocket.ItemData.ItemClass != ItemClass.Card)
                throw new Exception($"Item {itemToSocket.ProperName()} cannot be socketed.");

            var socketPosition = itemToSocket.ItemData.Position;
            var validItems = new List<InventoryItem>();
            var inventory = NetworkManager.Instance.PlayerState.Inventory;
            foreach (var (_, item) in inventory.GetInventoryData())
            {
                if (item.Type != ItemType.UniqueItem)
                    continue;
                if (((UniqueItemFlags)item.UniqueItem.Flags & UniqueItemFlags.CraftedItem) > 0)
                    continue;
                if (item.IsAvailableForSocketing(socketPosition))
                    validItems.Add(item);
            }

            if (validItems.Count == 0)
            {
                CameraFollower.Instance.AppendError($"No equipment available that can socket this card.");
                return;
            }

            var prefab = UiManager.Instance.GenericItemListV2Prefab;
            var container = UiManager.Instance.PrimaryUserWindowContainer;
            var go = Instantiate(prefab, container);
            instance = go.AddComponent<CardSocketWindow>();

            //center window
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 400);
            var parentContainer = (RectTransform)go.transform.parent;
            var middle = parentContainer.rect.size / 2f;
            middle = new Vector2(middle.x, -middle.y);
            rect.anchoredPosition = middle - new Vector2(rect.sizeDelta.x / 2, -rect.sizeDelta.y / 2);

            instance.Init(go.GetComponent<GenericItemListV2>(), itemToSocket, validItems);
        }
    }
}
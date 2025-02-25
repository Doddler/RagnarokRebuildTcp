using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Inventory
{
    public class ItemDescriptionWindow : WindowBase
    {
        public Sprite DefaultItemPortrait;
        public Image PortraitContainer;
        public TextMeshProUGUI ItemName;
        public TextMeshProUGUI ItemDescription;
        public RectTransform WindowRect;

        public GameObject CardSocketPanel;

        public Button ShowIllustrationButton;

        public Sprite CardSlotOpen;
        public Sprite CardSlotClosed;

        public List<DraggableItem> CardSocketEntries;

        private InventoryItem inventoryItem;
        private bool isInit;

        private void Init()
        {
            if (isInit)
                return;

            CardSocketEntries[0].OnRightClick = () => RightClickCardSlot(0);
            CardSocketEntries[1].OnRightClick = () => RightClickCardSlot(1);
            CardSocketEntries[2].OnRightClick = () => RightClickCardSlot(2);
            CardSocketEntries[3].OnRightClick = () => RightClickCardSlot(3);

            isInit = true;
        }

        public void ClickCardIllustrationButton()
        {
            var win = UiManager.Instance.CardIllustrationWindow;
            if (win == null)
            {
                var go = Resources.Load<GameObject>("Card Illustration");
                var go2 = Instantiate(go, UiManager.Instance.PrimaryUserWindowContainer);
                win = go2.GetComponent<CardIllustrationWindow>();
                win.CenterWindow();
                win.HideWindow();
                UiManager.Instance.CardIllustrationWindow = win;
            }

            win.DisplayCard(inventoryItem.ItemData);
        }

        private void DisplayDescription(Sprite collection)
        {
            var item = inventoryItem.ItemData;

            ItemName.text = inventoryItem.ProperName();
            ItemDescription.text = ClientDataLoader.Instance.GetItemDescription(item.Code);
            PortraitContainer.sprite = collection;

            ShowWindow();
            MoveToTop();


            ShowIllustrationButton.gameObject.SetActive(item.ItemClass == ItemClass.Card);

            if (!item.IsUnique || item.Slots <= 0 || CardSocketEntries == null || CardSocketEntries.Count == 0)
            {
                CardSocketPanel.SetActive(false);
            }
            else
            {
                CardSocketPanel.SetActive(true);

                for (var i = 0; i < CardSocketEntries.Count; i++)
                {
                    var slot = inventoryItem.UniqueItem.SlotData(i);
                    if (slot > 1)
                    {
                        if (!ClientDataLoader.Instance.TryGetItemById(slot, out var socketed))
                            socketed = item; //lol
                        var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(socketed.Sprite);
                        CardSocketEntries[i].Assign(DragItemType.SocketedItem, sprite, socketed.Id, 1);
                    }
                    else
                    {
                        if (i < item.Slots)
                            CardSocketEntries[i].Assign(DragItemType.SocketedItem, CardSlotOpen, -1, 0);
                        else
                            CardSocketEntries[i].Assign(DragItemType.SocketedItem, CardSlotClosed, -1, 0);
                    }
                }
            }

            ItemDescription.ForceMeshUpdate();
            Vector2 preferredDimensions = ItemDescription.GetPreferredValues(415, 0); //300 minus 20 for margins
            WindowRect.sizeDelta = new Vector2(626, Mathf.Max(246, preferredDimensions.y + 70));
        }

        public void RightClickCardSlot(int slot)
        {
            var id = CardSocketEntries[slot].ItemId;
            if (id > 0)
                UiManager.Instance.SubDescriptionWindow.ShowItemDescription(id);
        }

        public void ShowItemDescription(InventoryItem item)
        {
            Init();

            inventoryItem = item;
            var collectionPath = $"Assets/Sprites/Imported/Collections/{item.ItemData.Sprite}.png";
            ShowIllustrationButton.gameObject.SetActive(false); //depending on how long it takes to load you could hit view illustration on an invalid item
            if (!ClientDataLoader.DoesAddressableExist<Sprite>(collectionPath))
                DisplayDescription(DefaultItemPortrait);
            else
                AddressableUtility.LoadSprite(gameObject, collectionPath, DisplayDescription);
        }

        public void ShowItemDescription(int itemId)
        {
            //we aren't related to an inventory item, so we'll have to fake it.
            var data = ClientDataLoader.Instance.GetItemById(itemId);
            ShowItemDescription(new InventoryItem() { BagSlotId = -1, ItemData = data });
        }
    }
}
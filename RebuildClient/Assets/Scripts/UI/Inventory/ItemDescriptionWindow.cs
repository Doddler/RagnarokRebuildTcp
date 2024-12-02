using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
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

        private InventoryItem inventoryItem;

        private void DisplayDescription(Sprite collection)
        {
            if (!ClientDataLoader.Instance.TryGetItemById(inventoryItem.ItemData.Id, out var item))
            {
                Debug.LogWarning($"ItemDescriptionWindow could not find an item description for {inventoryItem.ProperName()}");
                HideWindow();
                return;
            }

            ItemName.text = inventoryItem.ProperName();
            ItemDescription.text = ClientDataLoader.Instance.GetItemDescription(item.Code);
            PortraitContainer.sprite = collection;
            
            ShowWindow();
            MoveToTop();
            
            ItemDescription.ForceMeshUpdate();
            Vector2 preferredDimensions = ItemDescription.GetPreferredValues(415, 0); //300 minus 20 for margins
            WindowRect.sizeDelta = new Vector2(626, Mathf.Max(246, preferredDimensions.y+70));


        }

        public void ShowItemDescription(InventoryItem item)
        {
            inventoryItem = item;
            var collectionPath = $"Assets/Sprites/Imported/Collections/{item.ItemData.Sprite}.png";
            if (!ClientDataLoader.DoesAddressableExist<Sprite>(collectionPath))
                DisplayDescription(DefaultItemPortrait);
            else
                AddressableUtility.LoadSprite(gameObject, collectionPath, DisplayDescription);

        }
        
        public void ShowItemDescription(int itemId)
        {
            //we aren't related to an inventory item, so we'll have to fake it.
            var data = ClientDataLoader.Instance.GetItemById(itemId);
            ShowItemDescription(new InventoryItem() { BagSlotId = -1, ItemData = data});
        }

    }
}
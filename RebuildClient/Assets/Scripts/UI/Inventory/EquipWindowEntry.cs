using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Inventory
{
    public class EquipWindowEntry : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public Image Image;
        public TextMeshProUGUI ItemName;
        public Sprite Sprite;
        public int ItemId;
        public int ItemCount;
        public EquipSlot Slot;
        private InventoryItem inventoryItem;
        private bool isActive;

        public void RefreshSlot(InventoryItem item)
        {
            inventoryItem = item;
            Sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(item.ItemData.Sprite);
            Image.sprite = Sprite;
            Image.rectTransform.sizeDelta = Sprite.rect.size * 2;
            ItemName.text = item.ToString();
            
            Image.gameObject.SetActive(true);
            ItemName.gameObject.SetActive(true);
            isActive = true;
        }

        public void ClearSlot()
        {
            Image.gameObject.SetActive(false);
            ItemName.text = "";
            isActive = false;

        }
        
        public void OnDrag(PointerEventData eventData)
        {
            //why are we here?
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActive)
                return;
            UiManager.Instance.StartEquipmentDrag(inventoryItem, Sprite);
            // Image.gameObject.SetActive(false);
            // ItemName.gameObject.SetActive(false);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isActive)
                return;
            
            UiManager.Instance.EndItemDrag();
            // Image.gameObject.SetActive(true);
            // ItemName.gameObject.SetActive(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isActive || eventData.clickCount != 2)
                return;
            
            NetworkManager.Instance.SendUnEquipItem(inventoryItem.BagSlotId);
        }
    }
}
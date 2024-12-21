using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class ItemListEntry : DragItemBase, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public Image Background;
        public TextMeshProUGUI ItemName;
        public TextMeshProUGUI RightText;
        public ItemListRole Role;

        public void Assign(Sprite sprite, int itemId, int count)
        {
            var item = ClientDataLoader.Instance.GetItemById(itemId);
            ItemName.text = item.Name;
            if(item.Slots > 0)
                ItemName.text = $"{item.Name}[{item.Slots}]";
            base.Assign(DragItemType.ShopItem, sprite, itemId, count);
            if(count > 0)
                UpdateCount(count);
            else
                HideCount();
        }
        
        public void Assign(Sprite sprite, ItemData item, int bagId, int count)
        {
            ItemName.text = item.Name;
            if(item.Slots > 0)
                ItemName.text = $"{item.Name}[{item.Slots}]";
            base.Assign(DragItemType.ShopItem, sprite, bagId, count);
            if(count > 0)
                UpdateCount(count);
            else
                HideCount();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var dragCount = 0;
            if (Role == ItemListRole.SellToNpcSummary || Role == ItemListRole.SellToNpcItemList)
                dragCount = ItemCount; 
            UiManager.Instance.StartStoreItemDrag(ItemId, Sprite, Role, dragCount);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UiManager.Instance.EndItemDrag();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                
                if(Role == ItemListRole.BuyFromNpcItemList || Role == ItemListRole.BuyFromNpcSummary)
                    UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(ItemId);
                else
                    UiManager.Instance.ItemDescriptionWindow.ShowItemDescription(NetworkManager.Instance.PlayerState.Inventory.GetInventoryItem(ItemId));
                return;
            }

            //should, in theory, let you continue double-clicking the next item on the list
            if (eventData.clickCount % 2 == 1 && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) 
                return;
            
            ShopUI.Instance.OnDropItem(new ShopDragData() {DragSource = Role, DragOrigin = ItemDragOrigin.ShopWindow, ItemId = ItemId, Count = ItemCount});
        }

        public void OnDrag(PointerEventData eventData)
        {
            //this has to be here or OnBeginDrag or OnEndDrag don't work right
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Background.color = new Color(Background.color.r, Background.color.g, Background.color.b, 1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Background.color = new Color(Background.color.r, Background.color.g, Background.color.b, 0f);
        }
    }
}
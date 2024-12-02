using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class ItemListInteractionDropZone : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemDropTarget
    {
        public Image HighlightZone;
        public bool IsBuyingFromNPC;

        public ItemListWindow Parent;

        public new void Awake()
        {
            HighlightZone.color = new Color32(0, 0, 0, 0);
            base.Awake();
        }

        public void DisableDropArea()
        {
            HighlightZone.color = new Color32(0, 0, 0, 0);
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
            {
                var mode = UiManager.Instance.DragItemObject.Origin;
                if (mode != ItemDragOrigin.ItemWindow && mode != ItemDragOrigin.ShopWindow)
                    return;
                if (mode == ItemDragOrigin.ItemWindow && IsBuyingFromNPC)
                    return;
                
                UiManager.Instance.RegisterDragTarget(this);
                HighlightZone.color = new Color32(149, 190, 255, 255);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
            {
                UiManager.Instance.UnregisterDragTarget(this);
                HighlightZone.color = new Color32(0, 0, 0, 0);
            }

        }

        public void DropItem()
        {
            var obj = UiManager.Instance.DragItemObject;
            
            if (obj.Origin == ItemDragOrigin.ShopWindow)
            {
                var type = (ItemListRole)obj.OriginId;
                var shopData = new ShopDragData()
                {
                    ItemId = obj.ItemId,
                    DragOrigin = ItemDragOrigin.ShopWindow,
                    DragSource = type,
                    Count = obj.ItemCount
                };
                ShopUI.Instance.OnDropItem(shopData);
            }
        }
    }
}
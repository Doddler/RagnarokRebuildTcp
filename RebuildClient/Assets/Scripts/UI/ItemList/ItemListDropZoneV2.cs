using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class ItemListDropZoneV2 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemDropTarget
    {
        public Image HighlightZone;

        public Action OnDropItem;
        public ItemDragOrigin ValidDragOrigins;
        

        public void Awake()
        {
            HighlightZone.color = new Color32(0, 0, 0, 0);
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
                if ((mode & ValidDragOrigins) == 0)
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
            
            if (obj == null || (obj.Origin & ValidDragOrigins) == 0)
                return;

            OnDropItem?.Invoke();
        }
    }
}
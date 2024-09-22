using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class DragTrashBucket : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemDropTarget
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
                UiManager.Instance.RegisterDragTarget(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (UiManager.Instance.IsDraggingItem)
                UiManager.Instance.UnregisterDragTarget(this);
        }

        public void DropItem()
        {
            var obj = UiManager.Instance.DragItemObject;
            switch (obj.Origin)
            {
                case ItemDragOrigin.HotBar:
                    UiManager.Instance.SkillHotbar.GetEntryById(obj.OriginId).Clear();
                    break;
                case ItemDragOrigin.ItemWindow:
                    Debug.Log($"Dropped item from inventory onto ground area.");
                    NetworkManager.Instance.SendDropItem(obj.ItemId, obj.ItemCount);
                    break;
            }
        }
    }
}
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public enum DragItemType
    {
        None,
        Skill,
        Item
    }
    
    public class DraggableItem : DragItemBase, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private bool isMouseDown = false;
        private Vector3 startMousePosition;
        private Vector3 startPosition;
        private bool adjustCanvasAlpha;
        public ItemDragOrigin Origin;
        public int OriginId;
        public Action OnDoubleClick;

        private UiManager manager;

        public void Awake()
        {
            manager = UiManager.Instance;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            manager.StartItemDrag(this);
            manager.DragItemObject.Origin = ItemDragOrigin.HotBar;
            manager.DragItemObject.OriginId = OriginId;
            Image.enabled = false;
            CountText.enabled = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            manager.EndItemDrag();

            Image.enabled = true;
            CountText.enabled = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Debug.Log("HII");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Type != DragItemType.None && eventData.clickCount >= 2 && OnDoubleClick != null)
                OnDoubleClick();

        }
    }
}
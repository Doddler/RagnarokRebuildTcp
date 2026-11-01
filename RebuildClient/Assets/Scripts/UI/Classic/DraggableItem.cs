using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;


namespace Assets.Scripts.UI.Classic
{
    public class DraggableItem : IconAmount, IDragSource, IDragTarget
    {
        public RoButton Button;

        private void OnEnable()
        {
            if (Button != null)
            {
                Button.onPointerClick.AddListener(OnPointerClick);
            }
        }

        private void OnDisable()
        {
            if (Button != null)
            {
                Button.onPointerClick.RemoveListener(OnPointerClick);
            }
        }

        private void OnPointerClick(PointerEventData click)
        {
            if (click.button == PointerEventData.InputButton.Right)
                OnRightClick();
            else if (click.button == PointerEventData.InputButton.Left && click.clickCount == 2)
                OnDoubleClick();
        }

        private void OnRightClick()
        {
            Debug.Log("Right click");
        }

        private void OnDoubleClick()
        {
            Debug.Log("Double click");
        }

        public DragPayload CreatePayload()
        {
            var text = $"";
            return new DragPayload(this, Sprite, text, null);
        }

        public bool CanDrag()
        {
            return true;
        }

        public void OnDragComplete()
        {
        }

        public bool CanDrop(DragPayload payload)
        {
            return true;
        }

        public void OnDropComplete(DragPayload payload)
        {
            Debug.Log("ITEM DROPPED");
        }
    }
}
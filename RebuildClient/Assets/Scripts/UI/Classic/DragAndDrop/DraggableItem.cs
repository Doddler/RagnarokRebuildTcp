using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;


namespace Assets.Scripts.UI.Classic.DragAndDrop
{
    public class DraggableItem : IconAmount, IItemDragSource, IDragTarget
    {
        public RoButton Button;
        public int ItemId { get; set; }

        private void OnEnable()
        {
            if (Button == null)
                Button = GetComponentInChildren<RoButton>(true);
            if (Button == null && iconSprite != null)
                Button = iconSprite.gameObject.AddComponent<RoButton>();
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
            ClassicItemDescriptionWindow.ToggleItemDescription(ItemId);
        }

        private void OnDoubleClick()
        {
            ClassicEquipmentWindow.Instance?.EquipAutomatically(ItemId);
        }

        public DragPayload CreatePayload()
        {
            return new DragPayload(this, Sprite, Amount);
        }

        public bool CanDrag()
        {
            return ItemId > 0;
        }

        public void OnDragComplete()
        {
        }

        public bool CanDrop(DragPayload payload)
        {
            return payload.source is DraggableGearSlot gear && gear.ItemId > 0;
        }

        public void OnDropComplete(DragPayload payload)
        {
            if (payload.source is DraggableGearSlot gear) Assets.Scripts.Network.NetworkManager.Instance?.SendUnEquipItem(gear.ItemId);
        }
    }
}

using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RO_Flex_UI.Components;
using RO_Flex_UI.Components.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;

// using FlexDragAndDrop = Assets.Scripts.UI.Classic.DragAndDrop;

namespace Assets.Scripts.UI.Classic.DragAndDrop
{
    public class DraggableGearSlot : IconText, IItemDragSource, IDragTarget
    {
        public RebuildSharedData.Enum.EquipSlot Target;
        int itemId;
        int amount;
        public int ItemId => itemId;
        public int Amount => amount;

        [SerializeField] protected bool canReplaceOnDrop = false;
        private RO_Flex_UI.Components.TooltipTrigger tooltipTrigger;
        [SerializeField] private RoButton button;

        private void OnEnable()
        {
            if (button == null) button = GetComponentInChildren<RoButton>(true);
            if (button != null) button.onPointerClick.AddListener(OnPointerClick);
        }

        private void OnDisable()
        {
            if (button != null) button.onPointerClick.RemoveListener(OnPointerClick);
        }

        private void OnPointerClick(PointerEventData click)
        {
            if (click.button == PointerEventData.InputButton.Right && itemId > 0)
                ClassicItemDescriptionWindow.ToggleItemDescription(itemId);
            if (click.button == PointerEventData.InputButton.Left && click.clickCount == 2 && itemId > 0)
                NetworkManager.Instance?.SendUnEquipItem(itemId);
        }

        protected override void Start()
        {
            base.Start();

            tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
        }

        public DragPayload CreatePayload()
        {
            var data = PlayerState.Instance.Inventory.TryGetInventoryItem(ItemId, out var item);
            return new DragPayload(this, Sprite, $"{item.ItemData.Name}");
        }

        public bool CanDrag()
        {
            return !Empty;
        }

        public bool CanDrop(DragPayload payload) => payload.source is DraggableItem item && ClassicEquipmentWindow.Instance.CanEquip(item.ItemId, Target);
        public void OnDropComplete(DragPayload payload)
        {
            if (CanDrop(payload)) ClassicEquipmentWindow.Instance.Equip(((DraggableItem)payload.source).ItemId, Target);
        }
        public void OnDragComplete() { }

        public override void Clear()
        {
            base.Clear();
            itemId = 0;
            amount = 0;

            if (tooltipTrigger == null)
                tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
            if (tooltipTrigger != null)
                tooltipTrigger.Enabled = false;
        }

        public override void Assign(Sprite sprite, string text, string amount = "")
        {
            base.Assign(sprite, text, amount);
            // Clear hides the contents; assigning confirmed gear must show them again.
            SetActive(true);

            if (tooltipTrigger == null)
                tooltipTrigger = GetComponent<RO_Flex_UI.Components.TooltipTrigger>();
            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetText($"{text}: {amount}");
                tooltipTrigger.Enabled = true;
            }
        }

        public void Assign(Sprite sprite, string text, int amount, int itemId)
        {
            Assign(sprite, text, amount.ToString());
            this.itemId = itemId;
            this.amount = amount;
        }
    }
}
